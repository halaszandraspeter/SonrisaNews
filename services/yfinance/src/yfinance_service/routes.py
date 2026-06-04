"""HTTP routes. One endpoint per operation; no overload-by-query-param."""

from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, Path, Query, status

from yfinance_service.schemas import HealthResponse, Quote, QuotesResponse
from yfinance_service.service import QuoteService
from yfinance_service.settings import Settings

_ROUTE_TAG = "quotes"


def build_router(settings: Settings) -> APIRouter:
    """Build the API router with a single shared service instance.

    The service is created once per app (not per request) so the in-process
    TTL cache survives across requests.
    """
    service = QuoteService(settings)
    router = APIRouter()

    def get_service() -> QuoteService:
        return service

    @router.get(
        "/health",
        tags=["meta"],
        summary="Liveness probe",
        description="Returns 200 if the sidecar process is up. Does not call yfinance.",
        response_model=HealthResponse,
    )
    async def health() -> HealthResponse:
        return HealthResponse(status="ok")

    @router.get(
        "/quote",
        tags=[_ROUTE_TAG],
        summary="Single-symbol quote",
        description="Fetches a delayed quote for the given symbol. Returns 404 on unknown symbol.",
        response_model=Quote,
        responses={
            404: {"description": "Symbol not found"},
            503: {"description": "yfinance upstream unavailable"},
        },
    )
    async def get_quote(
        symbol: str = Query(..., min_length=1, max_length=10, examples=["AAPL"]),
        svc: QuoteService = Depends(get_service),
    ) -> Quote:
        quote = await svc.fetch_one(symbol)
        if quote is None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail={"code": "SymbolNotFound", "message": f"No quote for {symbol}"},
            )
        return quote

    @router.get(
        "/quotes",
        tags=[_ROUTE_TAG],
        summary="Batched quotes",
        description=(
            "Fetches a delayed quote for each symbol in the comma-separated list. "
            "The response is always 200; per-symbol errors are reported via a "
            "structured `errors` map and a `null` value in the `quotes` map."
        ),
        response_model=QuotesResponse,
        responses={503: {"description": "yfinance upstream unavailable"}},
    )
    async def get_quotes(
        symbols: str = Query(
            ...,
            description="Comma-separated list of symbols, e.g. AAPL,MSFT,GOOG",
            examples=["AAPL,MSFT,GOOG"],
        ),
        svc: QuoteService = Depends(get_service),
    ) -> QuotesResponse:
        symbol_list = [s.strip().upper() for s in symbols.split(",") if s.strip()]
        if not symbol_list:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail={"code": "EmptySymbols", "message": "Provide at least one symbol."},
            )
        return await svc.fetch_many(symbol_list)

    return router
