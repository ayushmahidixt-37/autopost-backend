from flask import Flask, request, jsonify, send_file
import subprocess as sp
sp.run(["apt-get", "update", "-qq"], capture_output=True)
sp.run(["apt-get", "install", "-y", "-qq", "ffmpeg"], capture_output=True)
from flask_cors import CORS
import anthropic, os, json, threading, schedule, time, subprocess, tempfile
from datetime import datetime
from google.oauth2.credentials import Credentials
from google.auth.transport.requests import Request
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload, MediaIoBaseDownload
import io

app = Flask(__name__)
CORS(app)

# ── SETUP ───────────────────────────────────
ANTHROPIC_KEY = os.environ.get("ANTHROPIC_API_KEY", "")
GOOGLE_TOKEN  = os.environ.get("GOOGLE_TOKEN", "")
DRIVE_FOLDER  = os.environ.get("DRIVE_FOLDER", "raw_videos")
YT_CHANNEL    = os.environ.get("YOUTUBE_CHANNEL", "")

client = anthropic.Anthropic(api_key=ANTHROPIC_KEY)

current_config = {
    "topic": "", "channel": "", "posts_per_day": 3,
    "privacy": "private", "active": False
}

pipeline_status = {
    "running": False, "step": "", "progress": 0,
    "last_run": None, "last_error": None,
    "videos_processed": 0, "videos_posted": 0
}

SYSTEM = """Tum AutoPost AI ho. Jab user topic de:
1. Better version suggest karo
2. 3 punchy captions do
3. 8 hashtags do
4. Confirm maango

Confirm hone par SIRF yeh JSON do:
```json
{"confirmed":true,"topic":"...","caption":"...","hashtags":["t1","t2","t3","t4","t5","t6","t7","t8"],"posts_per_day":3}
```
Chhote jawab. Hindi/English dono okay."""

# ── GOOGLE AUTH ──────────────────────────────
def get_credentials():
    if not GOOGLE_TOKEN:
        raise Exception("GOOGLE_TOKEN not set in Railway variables")
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
        raise Exception(f"Folder '{folder_name}' not found in Drive")
    return files[0]["id"]

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

def move_to_archive(drive, file_id, folder_id):
    archive_name = "archive_videos"
    try:
        archive_id = get_folder_id(drive, archive_name)
    except:
        meta = {"name": archive_name, "mimeType": "application/vnd.google-apps.folder"}
        archive = drive.files().create(body=meta, fields="id").execute()
        archive_id = archive["id"]
    drive.files().update(
        fileId=file_id,
        addParents=archive_id,
        removeParents=folder_id,
        fields="id,parents"
    ).execute()

# ── VIDEO PROCESSING ─────────────────────────
def add_overlays(input_path, output_path, caption, topic):
    caption_esc = caption.upper().replace("'", "").replace(":", "")[:50]
    topic_esc = topic[:30].replace("'", "").replace(":", "")
    font = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
    
    vf = (
        f"scale=1080:1920:force_original_aspect_ratio=increase,"
        f"crop=1080:1920,"
        f"drawbox=x=0:y=0:w=iw:h=70:color=#F5C518@0.92:t=fill,"
        f"drawtext=text='{topic_esc}':fontfile={font}:fontsize=26:fontcolor=black:x=(w-text_w)/2:y=22,"
        f"drawtext=text='{caption_esc}':fontfile={font}:fontsize=38:fontcolor=white:borderw=3:bordercolor=black:x=(w-text_w)/2:y=h-120,"
        f"drawtext=text='{YT_CHANNEL}':fontfile={font}:fontsize=22:fontcolor=white@0.65:x=w-text_w-18:y=h-44"
    )
    result = subprocess.run([
        "ffmpeg", "-y", "-i", input_path,
        "-vf", vf,
        "-c:v", "libx264", "-preset", "fast",
        "-c:a", "copy", output_path
    ], capture_output=True, text=True)
    if result.returncode != 0:
        raise Exception(f"FFmpeg failed with code {result.returncode}")

def upload_youtube(video_path, title, description, tags, privacy):
    youtube = get_youtube()
    body = {
        "snippet": {
            "title": title[:100],
            "description": description,
            "tags": tags,
            "categoryId": "22"
        },
        "status": {
            "privacyStatus": privacy,
            "selfDeclaredMadeForKids": False
        }
    }
    media = MediaFileUpload(video_path, mimetype="video/mp4", resumable=True)
    req = youtube.videos().insert(part="snippet,status", body=body, media_body=media)
    response = None
    while response is None:
        _, response = req.next_chunk()
    return response["id"]

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
    today = datetime.now().date().isoformat()
    history = load_history()
    posted_today = [p for p in history["posted"] if p.get("date") == today]
    max_per_day = current_config.get("posts_per_day", 3)

    try:
        if len(posted_today) >= max_per_day:
            pipeline_status["step"] = f"Daily cap reached ({max_per_day}/day)"
            pipeline_status["running"] = False
            return

        pipeline_status["step"] = "Connecting to Drive..."
        pipeline_status["progress"] = 10
        drive = get_drive()
        folder_id = get_folder_id(drive, DRIVE_FOLDER)

        pipeline_status["step"] = "Finding new videos..."
        pipeline_status["progress"] = 20
        videos = list_videos(drive, folder_id, history["processed_ids"])

        if not videos:
            pipeline_status["step"] = "No new videos in Drive folder"
            pipeline_status["running"] = False
            return

        video = videos[0]
        pipeline_status["step"] = f"Downloading: {video['name']}"
        pipeline_status["progress"] = 30

        with tempfile.TemporaryDirectory() as tmpdir:
            raw_path = os.path.join(tmpdir, "raw.mp4")
            processed_path = os.path.join(tmpdir, "processed.mp4")

            download_video(drive, video["id"], raw_path)
            pipeline_status["step"] = "Adding captions & watermark..."
            pipeline_status["progress"] = 50

            caption = current_config.get("caption", current_config["topic"][:50])
            add_overlays(raw_path, processed_path, caption, current_config["topic"])

            pipeline_status["step"] = "Uploading to YouTube..."
            pipeline_status["progress"] = 75

            hashtags = current_config.get("hashtags", [])
            desc = f"{current_config['topic']}\n\n" + " ".join(f"#{t}" for t in hashtags)
            title = current_config["topic"][:100]
            video_id = upload_youtube(
                processed_path, title, desc, hashtags,
                current_config.get("privacy", "private")
            )

            pipeline_status["step"] = "Moving to archive..."
            pipeline_status["progress"] = 90
            move_to_archive(drive, video["id"], folder_id)

            history["processed_ids"].append(video["id"])
            history["posted"].append({
                "file": video["name"], "video_id": video_id,
                "date": today, "topic": current_config["topic"]
            })
            save_history(history)

            pipeline_status["videos_posted"] += 1
            pipeline_status["step"] = f"Done! YouTube ID: {video_id}"
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
    data = request.json
    msgs = data.get("messages", [])
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
    history = load_history()
    today = datetime.now().date().isoformat()
    posted_today = len([p for p in history["posted"] if p.get("date") == today])
    return jsonify({
        **pipeline_status,
        "posted_today": posted_today,
        "max_per_day": current_config.get("posts_per_day", 3),
        "topic": current_config.get("topic", ""),
        "active": current_config.get("active", False)
    })

@app.route("/pipeline/run", methods=["POST"])
def trigger_pipeline():
    threading.Thread(target=run_pipeline, daemon=True).start()
    return jsonify({"status": "started"})

@app.route("/history", methods=["GET"])
def get_history():
    return jsonify(load_history())

@app.route("/status", methods=["GET"])
def status():
    return jsonify({"status": "running", "topic": current_config.get("topic")})

if __name__ == "__main__":
    threading.Thread(target=scheduler, daemon=True).start()
    port = int(os.environ.get("PORT", 8080))
    print(f"Server running on port {port}")
    app.run(host="0.0.0.0", port=port)
