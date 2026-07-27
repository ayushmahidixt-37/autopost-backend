from flask import Flask, request, jsonify, send_file
import subprocess as sp
sp.run(["apt-get", "update", "-qq"], capture_output=True)
sp.run(["apt-get", "install", "-y", "-qq", "ffmpeg"], capture_output=True)

from flask_cors import CORS
import anthropic, os, json, threading, schedule, time, tempfile, base64
from datetime import datetime
from PIL import Image, ImageDraw, ImageFont
from google.oauth2.credentials import Credentials
from google.auth.transport.requests import Request
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload, MediaIoBaseDownload
import io

app = Flask(__name__)
CORS(app)

# ── SETUP ────────────────────────────────────
ANTHROPIC_KEY = os.environ.get("ANTHROPIC_API_KEY", "")
GOOGLE_TOKEN  = os.environ.get("GOOGLE_TOKEN", "")
DRIVE_FOLDER  = os.environ.get("DRIVE_FOLDER", "raw_videos")
YT_CHANNEL    = os.environ.get("YOUTUBE_CHANNEL", "@YourChannel")
ROOT_FOLDER   = DRIVE_FOLDER  # root folder containing channel subfolders

client = anthropic.Anthropic(api_key=ANTHROPIC_KEY)

CONFIG_FILE = "/app/config.json"

def load_config():
    default = {
        "topic": "", "channel": "", "posts_per_day": 3,
        "privacy": "private", "active": False,
        "video_type": "shorts",
        "caption": "", "hashtags": [],
        "channel_folder": "",
        "show_banner": False,
        "show_caption": False,
        "show_watermark": True,
        "banner_position": 80,
    }
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE) as f:
                saved = json.load(f)
                default.update(saved)
        except:
            pass
    return default

def save_config(cfg):
    try:
        with open(CONFIG_FILE, "w") as f:
            json.dump(cfg, f, indent=2)
    except:
        pass

current_config = load_config()

pipeline_status = {
    "running": False, "step": "", "progress": 0,
    "last_run": None, "last_error": None,
    "videos_posted": 0
}

SYSTEM = """Tum AutoPost AI ho. Jab user topic de:
1. Better version suggest karo
2. 3 punchy captions do
3. 8 hashtags do
4. Video type poocho (Shorts ya Regular)
5. Confirm maango

Confirm hone par SIRF yeh JSON do:
```json
{"confirmed":true,"topic":"...","caption":"...","hashtags":["t1","t2","t3","t4","t5","t6","t7","t8"],"posts_per_day":3,"video_type":"shorts"}
```
Chhote jawab. Hindi/English dono okay."""

# ── GOOGLE AUTH ──────────────────────────────
def get_credentials():
    creds = Credentials.from_authorized_user_info(json.loads(GOOGLE_TOKEN))
    if creds.expired and creds.refresh_token:
        creds.refresh(Request())
    return creds

def get_drive():
    return build("drive", "v3", credentials=get_credentials())

def get_youtube():
    return build("youtube", "v3", credentials=get_credentials())

# ── DRIVE HELPERS ────────────────────────────
def get_folder_id(drive, folder_name):
    res = drive.files().list(
        q=f"name='{folder_name}' and mimeType='application/vnd.google-apps.folder' and trashed=false",
        fields="files(id,name)"
    ).execute()
    files = res.get("files", [])
    if not files:
        # Create it at root level
        meta = {"name": folder_name, "mimeType": "application/vnd.google-apps.folder"}
        folder = drive.files().create(body=meta, fields="id").execute()
        return folder["id"]
    return files[0]["id"]

def get_or_create_subfolder(drive, name, parent_id):
    """Get or create a subfolder inside a parent."""
    q = "'" + parent_id + "' in parents and name='" + name + "' and mimeType='application/vnd.google-apps.folder' and trashed=false"
    res = drive.files().list(q=q, fields="files(id,name)").execute()
    if res.get("files"):
        return res["files"][0]["id"]
    meta = {"name": name, "mimeType": "application/vnd.google-apps.folder", "parents": [parent_id]}
    return drive.files().create(body=meta, fields="id").execute()["id"]

def list_channel_folders(drive):
    """List subfolders inside raw_videos/ — each is a channel."""
    root_id = get_folder_id(drive, ROOT_FOLDER)
    q = "'" + root_id + "' in parents and mimeType='application/vnd.google-apps.folder' and trashed=false and name != 'archive'"
    res = drive.files().list(q=q, fields="files(id,name)").execute()
    return res.get("files", [])

def delete_old_archive(drive, channel_folder_id, days=3):
    """Delete archive files older than N days."""
    from datetime import timedelta
    archive_id = get_or_create_subfolder(drive, "archive", channel_folder_id)
    cutoff = (datetime.utcnow() - timedelta(days=days)).strftime("%Y-%m-%dT%H:%M:%SZ")
    q = "'" + archive_id + "' in parents and trashed=false and createdTime < '" + cutoff + "'"
    res = drive.files().list(q=q, fields="files(id,name)").execute()
    for f in res.get("files", []):
        drive.files().delete(fileId=f["id"]).execute()
        print(f"Auto-deleted from archive: {f['name']}")

def list_videos(drive, folder_id, processed_ids):
    res = drive.files().list(
        q=f"'{folder_id}' in parents and mimeType='video/mp4' and trashed=false",
        fields="files(id,name,size)"
    ).execute()
    all_videos = res.get("files", [])
    return [v for v in all_videos if v["id"] not in processed_ids]

def download_video(drive, file_id, dest_path):
    request = drive.files().get_media(fileId=file_id)
    with open(dest_path, "wb") as f:
        downloader = MediaIoBaseDownload(f, request, chunksize=10*1024*1024)
        done = False
        while not done:
            _, done = downloader.next_chunk()

def move_to_archive(drive, file_id, parent_folder_id):
    """Move file to archive/ inside the channel folder. Delete after 3 days."""
    archive_id = get_or_create_subfolder(drive, "archive", parent_folder_id)
    drive.files().update(
        fileId=file_id,
        addParents=archive_id,
        removeParents=parent_folder_id,
        fields="id,parents"
    ).execute()
    print(f"Moved to archive")

# ── CLAUDE VIDEO ANALYSIS ────────────────────
def extract_frames(video_path, num_frames=3):
    """Extract multiple frames from video for Claude to analyze."""
    frames = []
    duration_result = sp.run([
        "ffprobe", "-v", "error", "-show_entries", "format=duration",
        "-of", "default=noprint_wrappers=1:nokey=1", video_path
    ], capture_output=True, text=True)
    
    try:
        duration = float(duration_result.stdout.strip())
    except:
        duration = 30.0
    
    for i in range(num_frames):
        timestamp = duration * (i + 1) / (num_frames + 1)
        frame_path = f"{video_path}_frame_{i}.jpg"
        sp.run([
            "ffmpeg", "-y", "-i", video_path,
            "-ss", str(timestamp), "-frames:v", "1",
            "-q:v", "2", frame_path
        ], capture_output=True)
        if os.path.exists(frame_path):
            frames.append(frame_path)
    return frames

def analyze_video_with_claude(video_path, config):
    """Claude analyzes video frames and generates content."""
    video_type = config.get("video_type", "shorts")
    topic = config.get("topic", "")
    
    # Extract frames
    frame_paths = extract_frames(video_path, num_frames=3)
    
    content = []
    
    # Add frames for Claude to analyze
    for fp in frame_paths:
        if os.path.exists(fp):
            with open(fp, "rb") as img:
                img_b64 = base64.b64encode(img.read()).decode()
            content.append({
                "type": "image",
                "source": {"type": "base64", "media_type": "image/jpeg", "data": img_b64}
            })
    
    # Cleanup frame files
    for fp in frame_paths:
        if os.path.exists(fp):
            os.remove(fp)
    
    if video_type == "shorts":
        prompt = f"""Tum ek YouTube Shorts content expert ho.

Channel topic: "{topic}"
Channel: {YT_CHANNEL}
Video type: SHORTS (max 20 seconds, vertical 9:16)
Date: {datetime.now().strftime('%B %d, %Y')}

In video frames ko analyze karo aur generate karo:

1. HEADER TEXT: 3-5 words max, bold statement (video ke content se related)
2. CAPTION: Max 8 words, curiosity-gap style, do NOT reveal outcome
3. TITLE: Max 50 characters, punchy (SHORTS ke liye chhota title best hai)
4. DESCRIPTION: 2-3 lines max, topic explain karo + call to action
5. HASHTAGS: 8 hashtags - #Shorts zaroori include karo
6. BANNER: "SHORTS • {datetime.now().strftime('%b %d').upper()}"
7. THUMBNAIL_TEXT: 2-3 words jo thumbnail pe likhe jayein (bold, impactful)

SIRF JSON respond karo:
{{
  "header": "...",
  "caption": "...",
  "yt_title": "...",
  "yt_description": "...",
  "hashtags": ["Shorts", "tag2", "tag3", "tag4", "tag5", "tag6", "tag7", "tag8"],
  "banner_text": "...",
  "thumbnail_text": "..."
}}"""
    else:
        prompt = f"""Tum ek YouTube content expert ho.

Channel topic: "{topic}"
Channel: {YT_CHANNEL}
Video type: REGULAR VIDEO (horizontal 16:9, variable length)
Date: {datetime.now().strftime('%B %d, %Y')}

In video frames ko analyze karo aur generate karo:

1. TITLE: Max 70 characters, SEO-friendly, engaging
2. DESCRIPTION: 5-7 lines - intro, key points, call to action, links placeholder
3. CAPTION: 10-12 words, descriptive
4. HASHTAGS: 10 relevant hashtags (no #Shorts)
5. BANNER: Topic + date
6. THUMBNAIL_TEXT: 3-4 words, high impact

SIRF JSON respond karo:
{{
  "header": "...",
  "caption": "...",
  "yt_title": "...",
  "yt_description": "...",
  "hashtags": ["tag1", "tag2", "tag3", "tag4", "tag5", "tag6", "tag7", "tag8", "tag9", "tag10"],
  "banner_text": "...",
  "thumbnail_text": "..."
}}"""
    
    content.append({"type": "text", "text": prompt})
    
    response = client.messages.create(
        model="claude-sonnet-4-6",
        max_tokens=800,
        messages=[{"role": "user", "content": content}]
    )
    
    raw = response.content[0].text.strip()
    raw = raw.replace("```json", "").replace("```", "").strip()
    return json.loads(raw)

# ── THUMBNAIL GENERATOR ──────────────────────
def generate_thumbnail(metadata, video_type, output_path):
    """Generate a thumbnail image using PIL."""
    if video_type == "shorts":
        W, H = 1080, 1920
    else:
        W, H = 1280, 720
    
    # Dark background
    img = Image.new("RGB", (W, H), (12, 16, 28))
    draw = ImageDraw.Draw(img)
    
    try:
        font_big   = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 120 if video_type=="shorts" else 80)
        font_med   = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 60 if video_type=="shorts" else 40)
        font_small = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 40 if video_type=="shorts" else 28)
    except:
        font_big = font_med = font_small = ImageFont.load_default()
    
    # Accent bar top
    draw.rectangle([(0, 0), (W, 16)], fill=(0, 229, 204))
    
    # Thumbnail text (center)
    thumb_text = metadata.get("thumbnail_text", metadata.get("header", "")).upper()
    words = thumb_text.split()
    lines, current = [], ""
    for word in words:
        test = (current + " " + word).strip()
        bbox = draw.textbbox((0,0), test, font=font_big)
        if bbox[2]-bbox[0] <= W-100:
            current = test
        else:
            lines.append(current)
            current = word
    if current:
        lines.append(current)
    
    line_h = font_big.size + 20
    total_h = len(lines) * line_h
    start_y = (H - total_h) // 2 - 50
    
    for i, line in enumerate(lines):
        bbox = draw.textbbox((0,0), line, font=font_big)
        tw = bbox[2]-bbox[0]
        x = (W-tw)//2
        y = start_y + i*line_h
        # Shadow
        draw.text((x+4, y+4), line, font=font_big, fill=(0,0,0))
        draw.text((x, y), line, font=font_big, fill=(255,255,255))
    
    # Channel name bottom
    ch = YT_CHANNEL
    bbox = draw.textbbox((0,0), ch, font=font_small)
    tw = bbox[2]-bbox[0]
    draw.text(((W-tw)//2, H-100), ch, font=font_small, fill=(0,229,204))
    
    # Accent bar bottom
    draw.rectangle([(0, H-16), (W, H)], fill=(0,229,204))
    
    img.save(output_path, quality=95)
    return output_path

# ── VIDEO PROCESSING ─────────────────────────
def process_video(input_path, output_path, metadata, video_type):
    """Add overlays based on frontend toggle settings."""

    # Read toggle settings from current_config
    show_banner    = current_config.get("show_banner", False)
    show_caption   = current_config.get("show_caption", False)
    show_watermark = current_config.get("show_watermark", True)
    banner_pos     = current_config.get("banner_position", 80)

    def esc(s):
        s = str(s)[:55]
        for ch in ["'", '"', "\\", ":", "[", "]", "{", "}", "%", "\n"]:
            s = s.replace(ch, " ")
        return s.strip()

    caption = esc(metadata.get("caption", "").upper())
    banner  = esc(metadata.get("banner_text", "").upper())
    channel = esc(YT_CHANNEL)
    font    = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

    if video_type == "shorts":
        scale_filter = "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920"
        fs_cap, fs_ban, fs_cha = 38, 26, 22
    else:
        scale_filter = "scale=1920:1080:force_original_aspect_ratio=increase,crop=1920:1080"
        fs_cap, fs_ban, fs_cha = 48, 32, 28

    filters = [scale_filter]

    if show_banner and banner:
        filters.append("drawbox=x=0:y=" + str(banner_pos) + ":w=iw:h=70:color=#F5C518@0.90:t=fill")
        filters.append("drawtext=text='" + banner + "':fontfile=" + font + ":fontsize=" + str(fs_ban) + ":fontcolor=black:x=(w-text_w)/2:y=" + str(banner_pos + 18))

    if show_caption and caption:
        filters.append("drawtext=text='" + caption + "':fontfile=" + font + ":fontsize=" + str(fs_cap) + ":fontcolor=white:borderw=3:bordercolor=black:x=(w-text_w)/2:y=h-130")

    if show_watermark and channel:
        filters.append("drawtext=text='" + channel + "':fontfile=" + font + ":fontsize=" + str(fs_cha) + ":fontcolor=white@0.65:x=w-text_w-18:y=h-44")

    vf = ",".join(filters)
    
    cmd = [
        "ffmpeg", "-y", "-i", input_path,
        "-vf", vf,
        "-c:v", "libx264", "-preset", "ultrafast", "-crf", "28",
        "-c:a", "aac", "-b:a", "128k",
    ]
    
    # For shorts: trim to 20 seconds max
    if video_type == "shorts":
        cmd += ["-t", "20"]
    
    cmd.append(output_path)
    
    result = sp.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        raise Exception(f"FFmpeg failed: {result.stderr[-300:]}")

# ── YOUTUBE UPLOAD ───────────────────────────
def upload_to_youtube(video_path, thumbnail_path, metadata, config):
    youtube = get_youtube()
    
    hashtags = metadata.get("hashtags", [])
    description = metadata["yt_description"] + "\n\n" + " ".join(f"#{t}" for t in hashtags)
    
    body = {
        "snippet": {
            "title":       metadata["yt_title"][:100],
            "description": description,
            "tags":        hashtags,
            "categoryId":  "17"
        },
        "status": {
            "privacyStatus":           config.get("privacy", "private"),
            "selfDeclaredMadeForKids": False
        }
    }
    
    media   = MediaFileUpload(video_path, mimetype="video/mp4", resumable=True)
    request = youtube.videos().insert(part="snippet,status", body=body, media_body=media)
    response = None
    while response is None:
        _, response = request.next_chunk()
    
    video_id = response["id"]
    
    # Set custom thumbnail
    if thumbnail_path and os.path.exists(thumbnail_path):
        try:
            youtube.thumbnails().set(
                videoId=video_id,
                media_body=MediaFileUpload(thumbnail_path, mimetype="image/jpeg")
            ).execute()
        except Exception as e:
            print(f"Thumbnail set failed: {e}")
    
    return video_id

# ── HISTORY ──────────────────────────────────
HISTORY_FILE = "/app/history.json"

def load_history():
    if os.path.exists(HISTORY_FILE):
        with open(HISTORY_FILE) as f:
            return json.load(f)
    return {"processed_ids": [], "posted": []}

def save_history(h):
    with open(HISTORY_FILE, "w") as f:
        json.dump(h, f, indent=2)

# ── MAIN PIPELINE ────────────────────────────
def run_pipeline():
    global pipeline_status
    if pipeline_status["running"]:
        return
    if not current_config.get("active") or not current_config.get("topic"):
        return
    
    pipeline_status["running"] = True
    pipeline_status["last_error"] = None
    today   = datetime.now().date().isoformat()
    history = load_history()
    posted_today = [p for p in history["posted"] if p.get("date") == today]
    max_per_day  = current_config.get("posts_per_day", 3)
    video_type   = current_config.get("video_type", "shorts")
    
    try:
        if len(posted_today) >= max_per_day:
            pipeline_status["step"] = f"Daily cap reached ({max_per_day}/day)"
            return

        pipeline_status["step"] = "Connecting to Drive..."
        pipeline_status["progress"] = 10
        drive = get_drive()

        # Use channel subfolder if specified, else auto-pick first available
        channel_name = current_config.get("channel_folder", "")
        if not channel_name:
            folders = list_channel_folders(drive)
            if folders:
                channel_name = folders[0]["name"]
                current_config["channel_folder"] = channel_name
                print(f"Auto-selected channel: {channel_name}")

        if channel_name:
            root_id = get_folder_id(drive, ROOT_FOLDER)
            folder_id = get_or_create_subfolder(drive, channel_name, root_id)
            delete_old_archive(drive, folder_id, days=3)
        else:
            folder_id = get_folder_id(drive, DRIVE_FOLDER)

        pipeline_status["step"] = "Finding new videos..."
        pipeline_status["progress"] = 20
        videos = list_videos(drive, folder_id, history["processed_ids"])

        if not videos:
            pipeline_status["step"] = "No new videos in Drive folder"
            return

        video = videos[0]

        with tempfile.TemporaryDirectory() as tmpdir:
            raw_path       = os.path.join(tmpdir, "raw.mp4")
            processed_path = os.path.join(tmpdir, "processed.mp4")
            thumb_path     = os.path.join(tmpdir, "thumbnail.jpg")

            pipeline_status["step"] = f"Downloading: {video['name']}"
            pipeline_status["progress"] = 30
            download_video(drive, video["id"], raw_path)

            pipeline_status["step"] = "Claude is analyzing video..."
            pipeline_status["progress"] = 45
            metadata = analyze_video_with_claude(raw_path, current_config)
            print(f"Analysis: {metadata.get('yt_title')}")

            pipeline_status["step"] = "Generating thumbnail..."
            pipeline_status["progress"] = 55
            generate_thumbnail(metadata, video_type, thumb_path)

            pipeline_status["step"] = "Adding captions, banner & watermark..."
            pipeline_status["progress"] = 65
            process_video(raw_path, processed_path, metadata, video_type)

            pipeline_status["step"] = "Uploading to YouTube..."
            pipeline_status["progress"] = 80
            video_id = upload_to_youtube(processed_path, thumb_path, metadata, current_config)

            pipeline_status["step"] = "Moving to archive..."
            pipeline_status["progress"] = 90
            move_to_archive(drive, video["id"], folder_id)

            history["processed_ids"].append(video["id"])
            history["posted"].append({
                "file":     video["name"],
                "video_id": video_id,
                "date":     today,
                "topic":    current_config["topic"],
                "type":     video_type,
                "title":    metadata.get("yt_title", "")
            })
            save_history(history)

            pipeline_status["videos_posted"] += 1
            pipeline_status["step"]     = f"Done! https://youtube.com/watch?v={video_id}"
            pipeline_status["progress"] = 100
            pipeline_status["last_run"] = datetime.now().isoformat()

    except Exception as e:
        pipeline_status["last_error"] = str(e)
        pipeline_status["step"] = f"Error: {str(e)[:100]}"
    finally:
        pipeline_status["running"] = False

# ── SCHEDULER ────────────────────────────────
def scheduler():
    schedule.every().day.at("09:00").do(run_pipeline)
    schedule.every().day.at("13:00").do(run_pipeline)
    schedule.every().day.at("18:00").do(run_pipeline)
    while True:
        schedule.run_pending()
        time.sleep(60)

# ── API ROUTES ───────────────────────────────
@app.route("/")
def index():
    return send_file("index.html")

@app.route("/chat", methods=["POST"])
def chat():
    data     = request.json
    msgs     = data.get("messages", [])
    response = client.messages.create(
        model="claude-sonnet-4-6", max_tokens=800,
        system=SYSTEM, messages=msgs
    )
    return jsonify({"reply": response.content[0].text})

@app.route("/config", methods=["GET"])
def get_config():
    return jsonify(current_config)

@app.route("/config", methods=["POST"])
def set_config():
    global current_config
    current_config.update(request.json)
    if current_config.get("active"):
        threading.Thread(target=run_pipeline, daemon=True).start()
    return jsonify({"status": "ok"})

@app.route("/pipeline/status", methods=["GET"])
def pipeline_status_route():
    history     = load_history()
    today       = datetime.now().date().isoformat()
    posted_today = len([p for p in history["posted"] if p.get("date") == today])
    return jsonify({
        **pipeline_status,
        "posted_today": posted_today,
        "max_per_day":  current_config.get("posts_per_day", 3),
        "topic":        current_config.get("topic", ""),
        "video_type":   current_config.get("video_type", "shorts"),
        "active":       current_config.get("active", False)
    })

@app.route("/pipeline/run", methods=["POST"])
def trigger_pipeline():
    threading.Thread(target=run_pipeline, daemon=True).start()
    return jsonify({"status": "started"})

@app.route("/history", methods=["GET"])
def get_history():
    return jsonify(load_history())

@app.route("/channels", methods=["GET"])
def get_channels():
    try:
        drive = get_drive()
        folders = list_channel_folders(drive)
        return jsonify({"channels": [f["name"] for f in folders]})
    except Exception as e:
        return jsonify({"channels": [], "error": str(e)})

@app.route("/status", methods=["GET"])
def status():
    return jsonify({"status": "running", "topic": current_config.get("topic")})

if __name__ == "__main__":
    threading.Thread(target=scheduler, daemon=True).start()
    port = int(os.environ.get("PORT", 8080))
    print(f"Server running on port {port}")
    app.run(host="0.0.0.0", port=port)
