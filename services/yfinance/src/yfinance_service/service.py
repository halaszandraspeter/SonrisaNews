"""Quote service. Wave 1: returns a deterministic fake — real yfinance is wired in wave 7.

The interface is the seam: `fetch_one` / `fetch_many` are what the C# worker
calls. The C# side has zero awareness of whether the data comes from a real
upstream or a fake.
"""

from __future__ import annotations

import asyncio
import hashlib
import time
from datetime import datetime, timezone

import structlog

from yfinance_service.schemas import Quote, QuotesResponse
from yfinance_service.settings import Settings

_log = structlog.get_logger("yfinance_service.service")


class QuoteService:
    """In-memory quote service with a TTL cache and a fake upstream.

    The fake is a hash of the symbol so the response is deterministic across
    restarts. Wave 7 swaps the fake for real yfinance behind this same class.
    """

    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._cache: dict[str, tuple[Quote, float]] = {}

    async def fetch_one(self, symbol: str) -> Quote | None:
        """Fetch a single quote. Returns None on unknown symbol."""
        symbol = symbol.upper()
        cached = self._get_cached(symbol)
        if cached is not None:
            return cached

        # Simulate the network round-trip — keeps callers honest about awaiting.
        await asyncio.sleep(0.001)
        quote = _fake_quote(symbol)
        if quote is None:
            return None
        self._store_cached(symbol, quote)
        return quote

    async def fetch_many(self, symbols: list[str]) -> QuotesResponse:
        """Fetch many quotes. Bad symbols are reported per-symbol; HTTP status stays 200."""
        if not symbols:
            return QuotesResponse()

        results = await asyncio.gather(
            *(self.fetch_one(s) for s in symbols),
            return_exceptions=False,
        )

        quotes: dict[str, Quote | None] = {}
        errors: dict[str, str] = {}
        for symbol, quote in zip(symbols, results, strict=True):
            if quote is None:
                quotes[symbol] = None
                errors[symbol] = "SymbolNotFound"
            else:
                quotes[symbol] = quote

        if errors:
            _log.warning("quote_batch_partial_failure", failed=list(errors))

        return QuotesResponse(quotes=quotes, errors=errors)

    # --- cache helpers -----------------------------------------------------
    def _get_cached(self, symbol: str) -> Quote | None:
        entry = self._cache.get(symbol)
        if entry is None:
            return None
        quote, stored_at = entry
        if time.monotonic() - stored_at > self._settings.cache_ttl_seconds:
            del self._cache[symbol]
            return None
        return quote

    def _store_cached(self, symbol: str, quote: Quote) -> None:
        self._cache[symbol] = (quote, time.monotonic())


def _fake_quote(symbol: str) -> Quote | None:
    """Deterministic fake quote. Symbols that hash to the magic value are 'unknown'."""
    digest = hashlib.sha256(symbol.encode("utf-8")).digest()
    if digest[0] == 0x00:
        return None  # reserved: this symbol is "unknown" for the fake
    price = 10.0 + (digest[1] / 255.0) * 490.0
    change_pct = ((digest[2] / 255.0) * 20.0) - 10.0
    volume = digest[3] * 1_000_000
    return Quote(
        symbol=symbol,
        price=round(price, 2),
        change_pct=round(change_pct, 2),
        volume=volume,
        as_of=datetime.now(tz=timezone.utc),
    )
