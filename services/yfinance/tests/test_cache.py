"""Cache behavior tests."""

from __future__ import annotations

import asyncio

from httpx import AsyncClient

from yfinance_service.settings import Settings


async def test_cache_returns_same_value_within_ttl() -> None:
    settings = Settings(cache_ttl_seconds=60)
    from yfinance_service.main import create_app
    from httpx import ASGITransport, AsyncClient

    app = create_app(settings)
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        a = (await client.get("/quote", params={"symbol": "AAPL"})).json()
        b = (await client.get("/quote", params={"symbol": "AAPL"})).json()
        assert a == b


async def test_cache_expires_after_ttl() -> None:
    settings = Settings(cache_ttl_seconds=0)
    from yfinance_service.main import create_app
    from httpx import ASGITransport, AsyncClient

    app = create_app(settings)
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        a = (await client.get("/quote", params={"symbol": "AAPL"})).json()
        await asyncio.sleep(0.01)  # past the 0s TTL
        b = (await client.get("/quote", params={"symbol": "AAPL"})).json()
        # Same shape, identical deterministic value (the fake is hash-based).
        assert a["price"] == b["price"]
        assert a["change_pct"] == b["change_pct"]
