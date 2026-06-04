"""Unit tests for the quote service's faker."""

from __future__ import annotations

from yfinance_service.service import QuoteService
from yfinance_service.settings import Settings


async def test_fetch_one_returns_quote_for_known_symbol() -> None:
    svc = QuoteService(Settings())
    quote = await svc.fetch_one("AAPL")
    assert quote is not None
    assert quote.symbol == "AAPL"


async def test_fetch_many_returns_200_shape_with_per_symbol_status() -> None:
    svc = QuoteService(Settings())
    result = await svc.fetch_many(["AAPL", "MSFT", "GOOG"])
    # All succeed in the fake (none of those three hash to the 0x00 byte).
    assert result.errors == {}
    for symbol, quote in result.quotes.items():
        assert quote is not None
        assert quote.symbol == symbol
