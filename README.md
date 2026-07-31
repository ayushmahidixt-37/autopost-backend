# Consent Manager — Erasure Request MVP

Helps an individual in India exercise their right to correction/erasure
under the DPDP Act, 2023: pick a company, generate a request letter,
track it through a reminder → notice → complaint-prep escalation ladder,
and keep an evidence log of everything.

This is the MVP slice chosen out of a larger "Spam Intelligence & Privacy
Enforcement Platform" concept. See **Scope & honest limitations** below
before treating this as more than a working prototype.

## Stack

- Flask API (`server.py`)
- Supabase (Postgres + Auth) — `schema.sql` sets up tables and Row Level
  Security policies so each user can only ever see their own requests.
  Auth is handled entirely by Supabase Auth; this backend never stores a
  password.

## Setup

1. Create a Supabase project.
2. In the Supabase SQL editor, run `schema.sql`.
3. (Optional, local testing only) run `seed_companies_example.sql` — these
   are fictional placeholder rows, not real company contacts. Don't send
   anything to them.
4. Copy `.env.example` to `.env` and fill in `SUPABASE_URL` and
   `SUPABASE_ANON_KEY` from Project Settings → API.
5. `pip install -r requirements.txt`
6. `python server.py`

## API

All routes except `/auth/*` require `Authorization: Bearer <access_token>`
from a Supabase Auth session.

| Route | Method | Purpose |
|---|---|---|
| `/auth/signup` | POST | `{email, password, full_name}` |
| `/auth/login` | POST | `{email, password}` → returns `session.access_token` |
| `/companies` | GET | `?q=` search the shared privacy-contact directory |
| `/companies` | POST | Add a company (always created `verified: false`) |
| `/requests` | POST | `{company_id, full_name, data_categories, reason, authorization_confirmed}` — creates a request and returns the generated letter. `authorization_confirmed` must be `true`. |
| `/requests` | GET | List the current user's requests, each annotated with `due_stage` |
| `/requests/<id>` | GET | Request detail + full event timeline |
| `/requests/<id>/mark-sent` | POST | User confirms they sent the initial letter themselves |
| `/requests/<id>/next-letter` | GET | `?full_name=` — returns the letter for whichever escalation stage is currently due (or none) |
| `/requests/<id>/advance` | POST | Confirms the current due step was sent, moves to the next stage |
| `/requests/<id>/resolve` | POST | Marks the request resolved |
| `/requests/<id>/note` | POST | `{note}` — free-text addition to the timeline |

## Design decisions worth knowing about

- **Nothing is auto-sent.** The backend only ever generates letter text;
  the user reviews it and sends it themselves (email, portal, post). This
  was a deliberate choice — a bot that autonomously fires legal-sounding
  notices at companies on a user's behalf is a liability problem, not
  just an engineering one, until there's a real authorization/e-signature
  flow behind it.
- **The company directory is crowd-sourced and unverified by default.**
  Every row has a `verified` boolean that starts `false`. Don't present
  this data as authoritative in a UI without a clear "unverified" badge —
  a wrong privacy-contact email sent confidently is worse than no directory
  at all.
- **RLS does the access-control work, not the Flask routes.** Every
  authenticated request is executed through a Supabase client scoped to
  that user's own JWT, so Postgres enforces "you can only see your own
  requests" even if a route has a bug.
- **The 3/7/14-day escalation cadence is a product choice, not a legal
  deadline.** Every generated letter says so. The DPDP Act's actual
  response-timeline requirements are set by Rules under the Act — verify
  the current, notified version before hardening this into anything that
  claims legal authority.

## Scope & honest limitations (read this)

This is deliberately the smallest useful slice of the original concept,
not the whole platform:

- **No spam-intelligence backend exists yet** (no `spam_sources`,
  `spam_events`, `sender_registry`, or risk scoring) — despite being
  described as "already built" in the original project doc, none of that
  code or schema was present in this repository. If it exists elsewhere,
  it needs to be located and connected rather than assumed.
- **No mobile app** — building one that reads incoming SMS/calls to score
  senders in real time runs into Play Store's Sensitive Permissions
  policy; you'd need to qualify as a default SMS/Dialer/Caller-ID app,
  which is a real distribution hurdle, not just a coding task.
- **The spam-intelligence half of the original vision competes directly**
  with Truecaller (network effects at ~250M Indian users), DoT's Sanchar
  Saathi/Chakshu portal, and telco-native AI spam filters that Jio/Airtel
  ship at the network layer. That's a hard market to win head-on; the
  erasure-request tool built here avoids that fight entirely.
- **This backend itself becomes a Data Fiduciary** the moment real users'
  personal data and company-contact data start flowing through it —
  budget for DPDP compliance obligations (grievance officer, consent
  records, breach handling) as part of shipping this for real, not as a
  later cleanup task.
