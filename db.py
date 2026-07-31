import os
from supabase import create_client, Client

SUPABASE_URL = os.environ.get("SUPABASE_URL", "")
SUPABASE_ANON_KEY = os.environ.get("SUPABASE_ANON_KEY", "")


def get_anon_client() -> Client:
    """Unauthenticated client — used only for signup/login."""
    return create_client(SUPABASE_URL, SUPABASE_ANON_KEY)


def get_client_for_token(access_token: str) -> Client:
    """
    Client scoped to a specific user's access token, so every query goes
    through Postgres Row Level Security as that user — never the service
    role. This means the backend can't accidentally leak one user's
    requests to another, even if a route has a bug.
    """
    client = create_client(SUPABASE_URL, SUPABASE_ANON_KEY)
    client.postgrest.auth(access_token)
    return client
