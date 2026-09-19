# YouTube Upload — Credentials, Setup & Progress So Far

## ⚠️ Ground rule — read this first
This file documents *what* credentials the system needs and *how* they're used. It does **not** contain any real key/token values, and it never should.

Actual secrets live only in your deploy platform's environment variables (Railway, per `nixpacks.toml`) or in a local `.env` file that is git-ignored — **never** in a file committed to this repo. `football_shorts_base (1).py` already has a real Anthropic key, a Pexels key, and a RapidAPI key hardcoded in plaintext, committed across several commits in git history. That is a live exposure — **rotate those three keys now**, independent of anything else in this doc, and delete them from that file in favor of `os.environ.get(...)`, matching how `server.py` already does it.

## 1. What credentials the system needs

| Env var | What it is | Used for |
|---|---|---|
| `ANTHROPIC_API_KEY` | Your Anthropic API key | Claude calls: video-frame analysis → title/description/hashtags, the `/chat` setup assistant, and growth recommendations |
| `GOOGLE_TOKEN` | A Google OAuth **authorized-user JSON blob** (contains a refresh token) for your default YouTube channel | Authenticates to Google Drive (source videos) and the YouTube Data API (upload) |
| `DRIVE_FOLDER` | Name of the Drive folder videos are pulled from (default: `raw_videos`) | Locating source footage |
| `YOUTUBE_CHANNEL` | Your channel handle, e.g. `@YourChannel` | Watermarking, thumbnails, description text |
| `CHANNEL_{N}_NAME`, `CHANNEL_{N}_TOKEN`, `CHANNEL_{N}_DRIVE` | Same idea as above, repeated per additional channel (`N` = 1, 2, 3...) | Multi-channel support — `server.py` auto-discovers these at startup |

Every one of these is read via `os.environ.get(...)` in `server.py` — nothing is hardcoded there. That's the correct pattern; `football_shorts_base (1).py` is the one file that breaks it.

## 2. How to actually get each credential

### Anthropic API key
1. Create/sign in at console.anthropic.com
2. Generate an API key
3. Set it as `ANTHROPIC_API_KEY` in Railway's environment variables — not in any file

### Google OAuth token (`GOOGLE_TOKEN`)
This is the one with more steps, because YouTube uploads require OAuth (not a plain API key):
1. In Google Cloud Console, create a project (or use an existing one).
2. Enable these APIs for that project: **YouTube Data API v3**, **Google Drive API**, and **YouTube Analytics API** (used for the post-upload performance loop).
3. Configure the OAuth consent screen (External is fine for personal use; you'll be the only test user while it's unverified).
4. Create an OAuth client ID of type **Desktop app**. Download the client secret JSON.
5. Run a one-time local OAuth flow (a short throwaway script using `google-auth-oauthlib`) with these scopes:
   - `https://www.googleapis.com/auth/youtube.upload`
   - `https://www.googleapis.com/auth/youtube.readonly`
   - `https://www.googleapis.com/auth/drive`
   - `https://www.googleapis.com/auth/yt-analytics.readonly`
   This opens a browser, you log in as the YouTube channel's owner account, and grant access.
6. The flow produces a `Credentials` object. Call `.to_json()` on it — that string (contains `refresh_token`, `client_id`, `client_secret`, scopes) is the value for `GOOGLE_TOKEN`.
7. Set that JSON string as an env var in Railway. `server.py`'s `get_credentials()` loads it with `Credentials.from_authorized_user_info(...)` and auto-refreshes it when expired (`creds.refresh(Request())`), so this is a one-time setup per channel, not a recurring task.
8. For each additional channel, repeat this whole flow against that channel's own Google account and set it as `CHANNEL_{N}_TOKEN`.

## 3. How the upload actually works today (`server.py`)
1. `get_credentials()` / `get_channel_credentials()` — loads the OAuth blob from the env var, refreshes if expired.
2. `get_drive()` / `get_youtube()` — build authenticated Google API clients from those credentials.
3. Pipeline (`run_pipeline()`): download a new video from the configured Drive folder → Claude analyzes extracted frames for title/description/hashtags → thumbnail generated (PIL) → ffmpeg overlays (banner/watermark/subtitles) → `upload_to_youtube()` calls `youtube.videos().insert(...)` with the metadata and uploads the file, then sets a custom thumbnail → source video moved to a Drive "archive" subfolder → result logged to `history.json`.
4. Runs automatically 3x/day via the built-in scheduler, or on-demand via `POST /pipeline/run`.
5. A separate scheduled job (`run_analytics_update`, daily) pulls YouTube Analytics for videos posted 3+ days earlier and feeds that into Claude-generated growth recommendations (`/analytics/<channel>/recommend`).

## 4. What we've done so far (progress log)

- **Repo audit**: reviewed `server.py`, `index.html`, `football_shorts_base (1).py`, deploy config. Confirmed the YouTube upload path is already built correctly (OAuth, not a workaround). Flagged the leaked-keys issue (still unresolved — see §1).
- **Product/business strategy**: agreed the goal is passive income via automated posting, scoped to YouTube first (Instagram automation was considered but shelved — no compliant third-party posting API exists for it without Meta App Review, and unofficial libraries risk account bans).
- **Planned 5-channel lineup** and an agent-based pipeline (Trend Agent → Content Generation Agent → Metadata Agent → automated Review/QA gate → Publish Agent → Monitoring Agent). Full detail in `PROJECT_SUMMARY.md`.
- **First channel greenlit and detailed**: kids comedy channel, branded **Funny Tales**. Full show bible in `docs/channels/gutli-kids-comedy.md` — kid character **Baba**, best-friend character **Gutli** (an intentionally original bear design, not modeled on any existing IP), the "only Baba can see Gutli move" world rule, episode format (Shorts, ~60–75s), recurring gags, and content-safety notes written as a checklist for the future Review/QA agent. Flagged that this show will need the **Made for Kids** classification regardless of its adult appeal, since that's a content-based determination, not a preference.
- **Three full sample episode scripts** drafted and validated against that format (`docs/channels/funny-tales-episode-scripts.md`) — proves the joke structure holds up across different premises before any production work starts.
- **Decisions locked**: full automation with no human approval gate, but an automated (non-human) Review/QA check stays in the pipeline as a safety net; template-based motion graphics + TTS chosen over full AI video generation for animated channels; comedy-channel content must be transformative, not straight reposts.

## 5. Not done yet
- Leaked keys in `football_shorts_base (1).py` — still need rotating and removing.
- No `.gitignore` existed in this repo until this change (added alongside this doc) — nothing sensitive should be one `git add .` away from a commit.
- Voice selection for Gutli/Baba — next decision before character design + first test render.
- The actual Content Generation Agent (script → TTS → motion graphics) — not built; today's pipeline only edits an already-sourced video, it doesn't generate one from scratch.
- Google Cloud project / OAuth credentials for the Funny Tales channel specifically haven't been set up yet — §2 above is the process to follow when ready.
