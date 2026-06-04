"""Smoke + shape tests for the yfinance sidecar."""

from __future__ import annotations

import pytest
from httpx import AsyncClient


async def test_health_returns_200(client: AsyncClient) -> None:
    r = await client.get("/health")
    assert r.status_code == 200
    assert r.json() == {"status": "ok"}


async def test_quote_returns_expected_shape(client: AsyncClient) -> None:
    r = await client.get("/quote", params={"symbol": "AAPL"})
    assert r.status_code == 200
    body = r.json()
    assert body["symbol"] == "AAPL"
    assert isinstance(body["price"], float)
    assert isinstance(body["change_pct"], float)
    assert isinstance(body["volume"], int)
    assert body["volume"] >= 0
    assert "as_of" in body


async def test_quote_is_deterministic_for_same_symbol(client: AsyncClient) -> None:
    a = (await client.get("/quote", params={"symbol": "AAPL"})).json()
    b = (await client.get("/quote", params={"symbol": "AAPL"})).json()
    assert a["price"] == b["price"]
    assert a["change_pct"] == b["change_pct"]


@pytest.mark.parametrize("symbol", ["AAPL", "msft", "TSLA"])
async def test_quote_normalizes_to_uppercase(client: AsyncClient, symbol: str) -> None:
    r = await client.get("/quote", params={"symbol": symbol})
    assert r.status_code == 200
    assert r.json()["symbol"] == symbol.upper()


async def test_quotes_batches_correctly(client: AsyncClient) -> None:
    r = await client.get("/quotes", params={"symbols": "AAPL,MSFT,GOOG"})
    assert r.status_code == 200
    body = r.json()
    assert set(body["quotes"].keys()) == {"AAPL", "MSFT", "GOOG"}
    for quote in body["quotes"].values():
        assert quote is not None
        assert {"price", "change_pct", "volume", "as_of"} <= set(quote)


async def test_quotes_empty_param_returns_400(client: AsyncClient) -> None:
    r = await client.get("/quotes", params={"symbols": ""})
    assert r.status_code == 400
