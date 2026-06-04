"""Pydantic request/response models. One model per shape."""

from __future__ import annotations

from datetime import datetime

from pydantic import BaseModel, Field


class Quote(BaseModel):
    """A single delayed quote."""

    symbol: str = Field(..., description="Ticker symbol, uppercased")
    price: float = Field(..., description="Last trade price (delayed up to 15 minutes)")
    change_pct: float = Field(
        ...,
        description="Percent change vs. the previous close, signed",
    )
    volume: int = Field(..., ge=0, description="Cumulative volume for the trading day")
    as_of: datetime = Field(..., description="UTC timestamp of the quote")


class QuotesResponse(BaseModel):
    """Response shape for `GET /quotes`. Per-symbol errors are reported via a `null` value in `quotes` and a structured entry in `errors`."""

    quotes: dict[str, Quote | None] = Field(
        default_factory=dict,
        description="Map from symbol to quote. A null value means the symbol failed; the reason is in `errors`.",
    )
    errors: dict[str, str] = Field(
        default_factory=dict,
        description="Map from symbol to error code. Empty when all symbols succeeded.",
    )


class HealthResponse(BaseModel):
    """Liveness response."""

    status: str = Field(..., description="Always 'ok' if the process is alive")
