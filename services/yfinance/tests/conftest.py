"""Pytest fixtures: a fresh FastAPI app per test + an httpx AsyncClient."""

from __future__ import annotations

from collections.abc import AsyncIterator

import pytest
from httpx import ASGITransport, AsyncClient

from yfinance_service.main import create_app
from yfinance_service.settings import Settings


@pytest.fixture
def settings() -> Settings:
    return Settings(environment="Test", cache_ttl_seconds=1)


@pytest.fixture
async def client(settings: Settings) -> AsyncIterator[AsyncClient]:
    app = create_app(settings)
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as ac:
        yield ac
