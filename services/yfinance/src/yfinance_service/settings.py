"""Application settings, loaded from environment variables."""

from __future__ import annotations

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """All runtime config. No hard-coded values anywhere else in the app."""

    model_config = SettingsConfigDict(
        env_prefix="YF__",
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
    )

    # Public base URL for the service (used in OpenAPI docs).
    base_url: str = "http://localhost:8001"

    # Environment name. Drives log format (JSON in prod, pretty in dev).
    environment: str = "Development"

    # In-process quote cache TTL. yfinance is rate-limited and the worker
    # polls every 5 minutes — a 60s cache is invisible to the user.
    cache_ttl_seconds: int = 60

    # Per-quote timeout against the yfinance upstream.
    upstream_timeout_seconds: float = 10.0
