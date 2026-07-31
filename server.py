import os
from datetime import datetime
from functools import wraps

from flask import Flask, request, jsonify, g
from flask_cors import CORS

from db import get_anon_client, get_client_for_token
import dpdp_templates as tmpl

app = Flask(__name__)
CORS(app)

ESCALATION_STAGE_AFTER_SENT = {
    # days elapsed since sent_at -> status to surface as "due"
    3: "reminder_due",
    7: "legal_notice_due",
    14: "complaint_prep_due",
}


# ── Auth ──────────────────────────────────────────────────────────────────
def require_auth(fn):
    @wraps(fn)
    def wrapper(*args, **kwargs):
        auth_header = request.headers.get("Authorization", "")
        if not auth_header.startswith("Bearer "):
            return jsonify({"error": "Missing bearer token"}), 401
        token = auth_header.removeprefix("Bearer ").strip()

        client = get_client_for_token(token)
        try:
            user = client.auth.get_user(token)
        except Exception:
            return jsonify({"error": "Invalid or expired token"}), 401
        if not user or not user.user:
            return jsonify({"error": "Invalid or expired token"}), 401

        g.db = client
        g.user_id = user.user.id
        g.user_email = user.user.email
        return fn(*args, **kwargs)

    return wrapper


@app.route("/auth/signup", methods=["POST"])
def signup():
    data = request.get_json(force=True) or {}
    email = data.get("email", "").strip()
    password = data.get("password", "")
    full_name = data.get("full_name", "").strip()
    if not email or not password:
        return jsonify({"error": "email and password are required"}), 400

    client = get_anon_client()
    try:
        res = client.auth.sign_up({
            "email": email,
            "password": password,
            "options": {"data": {"full_name": full_name}},
        })
    except Exception as e:
        return jsonify({"error": str(e)}), 400

    return jsonify({
        "user_id": res.user.id if res.user else None,
        "session": _serialize_session(res.session),
        "note": "Check your email to confirm your account if confirmation is required.",
    }), 201


@app.route("/auth/login", methods=["POST"])
def login():
    data = request.get_json(force=True) or {}
    email = data.get("email", "").strip()
    password = data.get("password", "")
    if not email or not password:
        return jsonify({"error": "email and password are required"}), 400

    client = get_anon_client()
    try:
        res = client.auth.sign_in_with_password({"email": email, "password": password})
    except Exception as e:
        return jsonify({"error": str(e)}), 401

    return jsonify({
        "user_id": res.user.id if res.user else None,
        "session": _serialize_session(res.session),
    })


def _serialize_session(session):
    if not session:
        return None
    return {
        "access_token": session.access_token,
        "refresh_token": session.refresh_token,
        "expires_at": session.expires_at,
    }


# ── Companies (privacy-contact directory) ──────────────────────────────────
@app.route("/companies", methods=["GET"])
@require_auth
def list_companies():
    query = request.args.get("q", "").strip()
    q = g.db.table("companies").select("*").order("name")
    if query:
        q = q.ilike("name", f"%{query}%")
    res = q.limit(50).execute()
    return jsonify({"companies": res.data})


@app.route("/companies", methods=["POST"])
@require_auth
def add_company():
    data = request.get_json(force=True) or {}
    name = data.get("name", "").strip()
    if not name:
        return jsonify({"error": "name is required"}), 400

    row = {
        "name": name,
        "category": data.get("category"),
        "privacy_email": data.get("privacy_email"),
        "grievance_email": data.get("grievance_email"),
        "dpo_email": data.get("dpo_email"),
        "website": data.get("website"),
        "notes": data.get("notes"),
        "source_url": data.get("source_url"),
        "verified": False,  # crowd-sourced entries always start unverified
        "created_by": g.user_id,
    }
    res = g.db.table("companies").insert(row).execute()
    return jsonify({"company": res.data[0] if res.data else None}), 201


# ── Erasure requests ─────────────────────────────────────────────────────
@app.route("/requests", methods=["POST"])
@require_auth
def create_request():
    data = request.get_json(force=True) or {}
    company_id = data.get("company_id")
    full_name = data.get("full_name", "").strip()
    data_categories = data.get("data_categories", [])
    reason = data.get("reason")
    authorization_confirmed = bool(data.get("authorization_confirmed"))

    if not company_id or not full_name:
        return jsonify({"error": "company_id and full_name are required"}), 400
    if not authorization_confirmed:
        return jsonify({
            "error": "authorization_confirmed must be true — you must confirm "
                     "you are requesting erasure of your own personal data "
                     "before a request can be created."
        }), 400

    company_res = g.db.table("companies").select("*").eq("id", company_id).execute()
    if not company_res.data:
        return jsonify({"error": "company not found"}), 404
    company = company_res.data[0]

    req_row = {
        "user_id": g.user_id,
        "company_id": company_id,
        "data_categories": data_categories,
        "reason": reason,
        "authorization_confirmed": True,
        "status": "draft",
    }
    req_res = g.db.table("erasure_requests").insert(req_row).execute()
    req = req_res.data[0]

    letter = tmpl.build_initial_request(
        full_name=full_name,
        email=g.user_email,
        company_name=company["name"],
        data_categories=data_categories,
        reason=reason,
    )

    _log_event(req["id"], "created", "Request created.")
    _log_event(req["id"], "letter_generated", "Initial erasure request letter generated.")

    return jsonify({"request": req, "letter": letter}), 201


@app.route("/requests", methods=["GET"])
@require_auth
def list_requests():
    res = (
        g.db.table("erasure_requests")
        .select("*, companies(name, category)")
        .eq("user_id", g.user_id)
        .order("created_at", desc=True)
        .execute()
    )
    requests_out = [_with_due_stage(r) for r in res.data]
    return jsonify({"requests": requests_out})


@app.route("/requests/<request_id>", methods=["GET"])
@require_auth
def get_request(request_id):
    req = _load_owned_request(request_id)
    if not req:
        return jsonify({"error": "not found"}), 404

    events_res = (
        g.db.table("request_events")
        .select("*")
        .eq("request_id", request_id)
        .order("created_at")
        .execute()
    )
    return jsonify({"request": _with_due_stage(req), "timeline": events_res.data})


@app.route("/requests/<request_id>/mark-sent", methods=["POST"])
@require_auth
def mark_sent(request_id):
    req = _load_owned_request(request_id)
    if not req:
        return jsonify({"error": "not found"}), 404

    now = datetime.utcnow().isoformat()
    g.db.table("erasure_requests").update({
        "status": "sent", "sent_at": now, "last_stage_at": now,
    }).eq("id", request_id).execute()
    _log_event(request_id, "marked_sent", "User confirmed the letter was sent to the company.")
    return jsonify({"status": "ok"})


@app.route("/requests/<request_id>/next-letter", methods=["GET"])
@require_auth
def next_letter(request_id):
    """
    Returns the letter text for whatever escalation stage is currently due.
    Nothing is auto-sent — the user copies this and sends it themselves,
    then calls the matching /advance endpoint to log it.
    """
    req = _load_owned_request(request_id)
    if not req:
        return jsonify({"error": "not found"}), 404
    if not req.get("sent_at"):
        return jsonify({"error": "the initial request has not been marked as sent yet"}), 400

    company = g.db.table("companies").select("name").eq("id", req["company_id"]).execute().data[0]
    events = (
        g.db.table("request_events").select("*")
        .eq("request_id", request_id).order("created_at").execute().data
    )
    full_name = request.args.get("full_name", "").strip()
    if not full_name:
        return jsonify({"error": "full_name query param is required"}), 400

    stage = _current_due_stage(req)
    sent_date = _fmt_date(req["sent_at"])

    if stage == "reminder_due":
        letter = tmpl.build_reminder(
            full_name=full_name, email=g.user_email,
            company_name=company["name"], original_sent_date=sent_date,
        )
    elif stage == "legal_notice_due":
        reminder_date = _find_event_date(events, "reminder_marked_sent") or sent_date
        letter = tmpl.build_legal_notice(
            full_name=full_name, email=g.user_email, company_name=company["name"],
            original_sent_date=sent_date, reminder_sent_date=reminder_date,
        )
    elif stage == "complaint_prep_due":
        reminder_date = _find_event_date(events, "reminder_marked_sent") or sent_date
        notice_date = _find_event_date(events, "legal_notice_marked_sent") or sent_date
        letter = tmpl.build_complaint_prep_notes(
            full_name=full_name, email=g.user_email, company_name=company["name"],
            original_sent_date=sent_date, reminder_sent_date=reminder_date,
            legal_notice_sent_date=notice_date,
        )
    else:
        return jsonify({"status": stage, "letter": None,
                         "note": "No escalation step is due yet."})

    return jsonify({"status": stage, "letter": letter})


@app.route("/requests/<request_id>/advance", methods=["POST"])
@require_auth
def advance_stage(request_id):
    req = _load_owned_request(request_id)
    if not req:
        return jsonify({"error": "not found"}), 404

    stage = _current_due_stage(req)
    event_map = {
        "reminder_due": ("reminder_marked_sent", "reminder_sent"),
        "legal_notice_due": ("legal_notice_marked_sent", "legal_notice_sent"),
        "complaint_prep_due": ("complaint_prep_generated", "complaint_prepared"),
    }
    if stage not in event_map:
        return jsonify({"error": f"nothing due to advance (current stage: {stage})"}), 400

    event_type, new_status = event_map[stage]
    now = datetime.utcnow().isoformat()
    g.db.table("erasure_requests").update({
        "status": new_status, "last_stage_at": now,
    }).eq("id", request_id).execute()
    _log_event(request_id, event_type, f"Marked '{stage}' step complete.")
    return jsonify({"status": new_status})


@app.route("/requests/<request_id>/resolve", methods=["POST"])
@require_auth
def resolve_request(request_id):
    req = _load_owned_request(request_id)
    if not req:
        return jsonify({"error": "not found"}), 404
    data = request.get_json(silent=True) or {}
    now = datetime.utcnow().isoformat()
    g.db.table("erasure_requests").update({
        "status": "resolved", "resolved_at": now,
    }).eq("id", request_id).execute()
    _log_event(request_id, "resolved", data.get("note", "Marked resolved by user."))
    return jsonify({"status": "resolved"})


@app.route("/requests/<request_id>/note", methods=["POST"])
@require_auth
def add_note(request_id):
    req = _load_owned_request(request_id)
    if not req:
        return jsonify({"error": "not found"}), 404
    data = request.get_json(force=True) or {}
    note = data.get("note", "").strip()
    if not note:
        return jsonify({"error": "note is required"}), 400
    _log_event(request_id, "note", note)
    return jsonify({"status": "ok"})


# ── Helpers ─────────────────────────────────────────────────────────────
def _load_owned_request(request_id):
    res = g.db.table("erasure_requests").select("*").eq("id", request_id).execute()
    return res.data[0] if res.data else None


def _log_event(request_id, event_type, detail):
    g.db.table("request_events").insert({
        "request_id": request_id, "event_type": event_type, "detail": detail,
    }).execute()


def _current_due_stage(req):
    if not req.get("sent_at"):
        return None
    elapsed_days = (datetime.utcnow() - _parse_dt(req["sent_at"])).days
    status = req["status"]

    completed_order = ["sent", "reminder_sent", "legal_notice_sent", "complaint_prepared", "resolved"]
    if status in ("resolved", "withdrawn"):
        return None

    due_stage = None
    for threshold, stage in ESCALATION_STAGE_AFTER_SENT.items():
        if elapsed_days >= threshold:
            due_stage = stage
    if not due_stage:
        return None

    stage_order = {"reminder_due": 1, "legal_notice_due": 2, "complaint_prep_due": 3}
    status_progress = {
        "sent": 0, "reminder_due": 0, "reminder_sent": 1,
        "legal_notice_due": 1, "legal_notice_sent": 2,
        "complaint_prep_due": 2, "complaint_prepared": 3,
    }
    if stage_order[due_stage] <= status_progress.get(status, 0):
        return None
    return due_stage


def _with_due_stage(req):
    req = dict(req)
    req["due_stage"] = _current_due_stage(req)
    return req


def _parse_dt(value):
    return datetime.fromisoformat(value.replace("Z", "+00:00")).replace(tzinfo=None)


def _fmt_date(value):
    return _parse_dt(value).strftime("%d %B %Y")


def _find_event_date(events, event_type):
    for e in events:
        if e["event_type"] == event_type:
            return _fmt_date(e["created_at"])
    return None


@app.route("/", methods=["GET"])
def index():
    return jsonify({
        "status": "running",
        "service": "consent-manager-erasure-mvp",
        "note": "See README.md for setup and known limitations.",
    })


if __name__ == "__main__":
    port = int(os.environ.get("PORT", 8080))
    app.run(host="0.0.0.0", port=port)
