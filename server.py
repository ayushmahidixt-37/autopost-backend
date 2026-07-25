"""
AutoPost Server — serves frontend + handles Anthropic API calls.
Deploy on Railway. Access via: https://autopost-backend-production-ca6f.up.railway.app
"""

from flask import Flask, request, jsonify, send_file
from flask_cors import CORS
import anthropic, os, json, threading, schedule, time
from datetime import datetime

app = Flask(__name__)
CORS(app)

client = anthropic.Anthropic(api_key=os.environ.get("ANTHROPIC_API_KEY"))

current_config = {
    "topic": "",
    "channel": "",
    "posts_per_day": 3,
    "privacy": "private",
    "active": False
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

# ── SERVE FRONTEND ──────────────────────────
@app.route("/")
def index():
    return send_file("index.html")

# ── CLAUDE CHAT API ─────────────────────────
@app.route("/chat", methods=["POST"])
def chat():
    data = request.json
    messages = data.get("messages", [])
    
    response = client.messages.create(
        model="claude-sonnet-4-6",
        max_tokens=800,
        system=SYSTEM,
        messages=messages
    )
    
    return jsonify({
        "reply": response.content[0].text
    })

# ── CONFIG ──────────────────────────────────
@app.route("/config", methods=["GET"])
def get_config():
    return jsonify(current_config)

@app.route("/config", methods=["POST"])
def set_config():
    global current_config
    current_config.update(request.json)
    print(f"[{datetime.now()}] Config: {current_config['topic']}")
    return jsonify({"status": "ok"})

@app.route("/status", methods=["GET"])
def status():
    return jsonify({"status": "running", "config": current_config})

# ── SCHEDULER ───────────────────────────────
def run_pipeline():
    if not current_config.get("active"):
        return
    print(f"[{datetime.now()}] Pipeline running for: {current_config['topic']}")
    # Your pipeline code runs here

def scheduler():
    schedule.every().day.at("09:00").do(run_pipeline)
    schedule.every().day.at("13:00").do(run_pipeline)
    schedule.every().day.at("18:00").do(run_pipeline)
    while True:
        schedule.run_pending()
        time.sleep(60)

if __name__ == "__main__":
    threading.Thread(target=scheduler, daemon=True).start()
    port = int(os.environ.get("PORT", 5000))
    print(f"Server running on port {port}")
    app.run(host="0.0.0.0", port=port)
