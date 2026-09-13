# AutoPost AI — Project Summary

_Last updated: 2026-09-13_

This file tracks what has been analyzed and decided so far. No implementation work has started yet — this is the planning record.

## 1. Current repo state

- **Repo**: `ayushmahidixt-37/autopost-backend` on GitHub — this is the single source of truth (no other copy exists elsewhere).
- **Files** (flat, no folder structure yet):
  - `server.py` — Flask backend: pulls a video from a Google Drive folder, has Claude analyze frames to generate title/description/hashtags/banner text, overlays banner/watermark/subtitles (Whisper) via ffmpeg, uploads to YouTube via the Data API, archives the source file, logs to `history.json`, and later pulls YouTube Analytics to feed Claude-generated growth recommendations. Runs on a schedule (3x/day) or on-demand. Supports multiple YouTube channels via `CHANNEL_N_NAME`/`CHANNEL_N_TOKEN` env vars.
  - `index.html` — single-page dark UI: chat-based setup, content toggles, analytics dashboard, history.
  - `football_shorts_base (1).py` — older Colab-exported script for a separate football-shorts pipeline (RSS trend detection, Pexels stock footage, Edge-TTS voiceover, Whisper captions). Not wired into `server.py`.
  - `requirements.txt`, `nixpacks.toml` — dependencies and deploy config (Railway-style).

- **⚠️ Open security issue (unfixed)**: `football_shorts_base (1).py` has hardcoded plaintext secrets — a Pexels API key, a RapidAPI key, and what looks like a live Anthropic API key — committed across multiple commits in git history. **These should be rotated regardless of any other work**, and removed from the file in favor of env vars (matching the pattern already used in `server.py`).

## 2. Business goal

Build a passive-income system: fully automated pipeline that generates and posts videos/Shorts to YouTube (Instagram discussed early on, but the working plan has focused on YouTube), across **5 channels**, run by a set of cooperating agents rather than one monolithic script.

## 3. Planned channels

| # | Channel | Content approach |
|---|---|---|
| 1 | Animated | Script → TTS voiceover → template motion graphics / kinetic-text animation (not full AI video generation — chosen for cost/reliability/scale) |
| 2 | Kids learning | Same generation approach as #1. **Marked "Made for Kids"** per compliance decision — trades some monetization features (no personalized ads, limited comments) for COPPA/policy compliance |
| 3 | Comedy / mimicry shorts | **Transformative edits only** — original hooks, reaction/commentary beats, emoji/edit overlays on top of source clips. Explicitly **not** straight reposts: re-uploading someone else's clip with a watermark doesn't grant rights and risks copyright claims + YouTube's reused-content policy disqualifying the channel from monetization. Straight reposts only if explicit permission/licensing from the original creator is obtained. |
| 4 | News / trending highlights | Headline/fact-driven (RSS-sourced), voiceover over stock/generated visuals or text graphics — avoids using actual broadcast footage (rights risk) |
| 5 | "What users want" / R&D channel | Runs format/topic experiments; results feed back into the trend/content decisions for all 5 channels, not just itself |

## 4. Agent architecture (planned)

| Agent | Role | Status |
|---|---|---|
| Trend/Niche Agent | Decides what to make next per channel | Partially prototyped (RSS pull in `football_shorts_base (1).py`) |
| Content Generation Agent | Produces the raw video (channel-specific: template motion graphics for #1/#2, transformative edit for #3, headline-driven for #4) | Not built — current pipeline only edits an existing sourced video, doesn't generate one |
| Metadata Agent | Title, description, hashtags, "what's in this video" | **Already built** — `analyze_video_with_claude()` in `server.py` |
| Review/QA Agent | Automated (non-human) policy/copyright/quality check before publish — agreed as a safety net even under full autonomy | Not built |
| Publish Agent | Uploads, sets thumbnail, schedules | Already built |
| Monitoring Agent | Tracks views/retention/CTR, feeds the optimization loop | Already built (analytics + Claude recommendations), needs a "cold start" dampening rule (see below) |

## 5. Key decisions made

- **Full autonomy**: no human approval gate on publishing, for any channel — but an automated AI Review/QA check stays in the pipeline as a non-human safety net.
- **Kids channel**: will be marked "Made for Kids."
- **Video generation approach**: template-based motion graphics + TTS, not AI text-to-video generation (for #1 and #2).
- **Comedy channel sourcing**: transformative edits, not straight reposts, as the default/automated behavior.
- **New-channel monitoring**: because early view/retention data on a brand-new channel is noisy (reflects the algorithm "testing" more than content quality), the Monitoring Agent should not start making autonomous optimization decisions until each channel has a minimum sample (e.g., ~10–15 videos or a few weeks) — early underperformance should be treated as expected, not acted on.

## 6. Open items / not yet decided

- Technical build plan: what APIs/tools per channel, data model, orchestration between agents, what's reusable from `server.py` vs. net-new.
- Rotating and removing the leaked API keys from `football_shorts_base (1).py`.
- Repo structure cleanup (currently flat, no folders/modules).
- Content licensing confirmation for stock footage/music/TTS voices at commercial scale.
- AI-generated/synthetic content disclosure requirements (YouTube's synthetic content setting).

## 7. Environment note

Work so far has happened in a cloud session against the GitHub repo directly — no local copy exists yet. To work locally: `git clone https://github.com/ayushmahidixt-37/autopost-backend.git`.
