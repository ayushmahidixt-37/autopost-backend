-- Consent Manager: Erasure Request MVP
-- Run this in the Supabase SQL editor for your project.
-- Relies on Supabase Auth's built-in auth.users table for user identity.

create extension if not exists pgcrypto;

-- ── Companies (privacy-contact directory) ────────────────────────────────
-- Crowd-sourced and editable. `verified` defaults to false: nothing here
-- should be treated as confirmed-correct until someone checks it against
-- the company's actual privacy policy / grievance officer notice.
create table if not exists companies (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  category text,
  privacy_email text,
  grievance_email text,
  dpo_email text,
  website text,
  notes text,
  verified boolean not null default false,
  source_url text,
  created_by uuid references auth.users(id),
  created_at timestamptz not null default now()
);

create index if not exists companies_name_idx on companies using gin (to_tsvector('simple', name));

-- ── Erasure requests ──────────────────────────────────────────────────────
create table if not exists erasure_requests (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  company_id uuid not null references companies(id),
  data_categories text[] not null default '{}',
  reason text,
  status text not null default 'draft'
    check (status in (
      'draft', 'sent', 'reminder_due', 'reminder_sent',
      'legal_notice_due', 'legal_notice_sent',
      'complaint_prep_due', 'complaint_prepared',
      'resolved', 'withdrawn'
    )),
  authorization_confirmed boolean not null default false,
  sent_at timestamptz,
  last_stage_at timestamptz,
  resolved_at timestamptz,
  created_at timestamptz not null default now()
);

create index if not exists erasure_requests_user_idx on erasure_requests (user_id);

-- ── Timeline / evidence log ────────────────────────────────────────────────
create table if not exists request_events (
  id uuid primary key default gen_random_uuid(),
  request_id uuid not null references erasure_requests(id) on delete cascade,
  event_type text not null
    check (event_type in (
      'created', 'letter_generated', 'marked_sent',
      'reminder_generated', 'reminder_marked_sent',
      'legal_notice_generated', 'legal_notice_marked_sent',
      'complaint_prep_generated',
      'company_responded', 'resolved', 'withdrawn', 'note'
    )),
  detail text,
  created_at timestamptz not null default now()
);

create index if not exists request_events_request_idx on request_events (request_id);

-- ── Row Level Security ─────────────────────────────────────────────────────
alter table companies enable row level security;
alter table erasure_requests enable row level security;
alter table request_events enable row level security;

-- Any authenticated user can read/contribute to the shared company directory.
create policy if not exists "companies_select_authenticated" on companies
  for select using (auth.role() = 'authenticated');

create policy if not exists "companies_insert_authenticated" on companies
  for insert with check (auth.role() = 'authenticated');

-- Users can only ever see or modify their own erasure requests.
create policy if not exists "erasure_requests_owner_all" on erasure_requests
  for all using (auth.uid() = user_id) with check (auth.uid() = user_id);

-- Events are only visible/writable through the parent request's ownership.
create policy if not exists "request_events_owner_all" on request_events
  for all using (
    exists (
      select 1 from erasure_requests r
      where r.id = request_events.request_id and r.user_id = auth.uid()
    )
  ) with check (
    exists (
      select 1 from erasure_requests r
      where r.id = request_events.request_id and r.user_id = auth.uid()
    )
  );
