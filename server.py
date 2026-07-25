"""
AutoPost Server — connects frontend (AutoPost_AI_App.html) to backend pipeline.
Deploy this on Railway. It runs 24/7 and posts videos automatically.
"""

from flask import Flask, request, jsonify
from flask_cors import CORS
import json, os, threading, schedule, time
from datetime import datetime

app = Flask(__name__)
CORS(app)

# Current active config (set from frontend)
current_config = {
    "topic": "",
    "channel": "",
    "posts_per_day": 3,
    "privacy": "private",
    "active": False
}

# ── API ROUTES ──────────────────────────────

@app.route("/config", methods=["GET"])
def get_config():
    """Frontend reads current config."""
    return jsonify(current_config)

@app.route("/config", methods=["POST"])
def set_config():
    """Frontend sends new config when user confirms topic."""
    global current_config
    data = request.json
    current_config.update(data)
    print(f"[{datetime.now()}] Config updated: {current_config['topic']}")
    return jsonify({"status": "ok", "config": current_config})

@app.route("/status", methods=["GET"])
def status():
    """Frontend checks if pipeline is running."""
    return jsonify({
        "active": current_config["active"],
        "topic": current_config["topic"],
        "channel": current_config["channel"],
        "time": datetime.now().isoformat()
    })

@app.route("/trigger", methods=["POST"])
def trigger():
    """Manually trigger one pipeline run."""
    threading.Thread(target=run_pipeline_once).start()
    return jsonify({"status": "triggered"})

# ── PIPELINE RUNNER ─────────────────────────

def run_pipeline_once():
    """Run the video generation pipeline once."""
    if not current_config.get("active") or not current_config.get("topic"):
        print("Pipeline not active or no topic set.")
        return
    print(f"[{datetime.now()}] Running pipeline for: {current_config['topic']}")
    # Import and run your existing pipeline here
    try:
        # This calls your existing football_shorts_base code
        # Update this import to match your actual pipeline file
        os.system("python football_shorts_base__1_.py")
    except Exception as e:
        print(f"Pipeline error: {e}")

def run_scheduler():
    """Run pipeline at scheduled times every day."""
    schedule.every().day.at("09:00").do(run_pipeline_once)
    schedule.every().day.at("13:00").do(run_pipeline_once)
    schedule.every().day.at("18:00").do(run_pipeline_once)
    while True:
        schedule.run_pending()
        time.sleep(60)

# ── START ───────────────────────────────────

if __name__ == "__main__":
    # Start scheduler in background
    t = threading.Thread(target=run_scheduler, daemon=True)
    t.start()
    print("AutoPost Server started!")
    print("Scheduler running: 9am, 1pm, 6pm daily")
    app.run(host="0.0.0.0", port=int(os.environ.get("PORT", 5000)))
