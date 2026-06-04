"""Sonrisa News yfinance sidecar — FastAPI app entry point."""

from __future__ import annotations

from contextlib import asynccontextmanager
from typing import AsyncIterator

import structlog
import uvicorn
from fastapi import FastAPI

from yfinance_service.logging import configure_logging
from yfinance_service.routes import build_router
from yfinance_service.settings import Settings


def create_app(settings: Settings | None = None) -> FastAPI:
    """Application factory.

    A factory is used (not a module-level singleton) so tests can spin up an
    isolated app with overridden dependencies — see `tests/conftest.py`.
    """
    cfg = settings or Settings()

    @asynccontextmanager
    async def lifespan(_: FastAPI) -> AsyncIterator[None]:
        configure_logging(cfg.environment)
        log = structlog.get_logger("yfinance_service.lifespan")
        log.info("yfinance_sidecar_starting", base_url=cfg.base_url)
        yield
        log.info("yfinance_sidecar_stopped")

    app = FastAPI(
        title="Sonrisa News — yfinance sidecar",
        version="0.1.0-mvp",
        lifespan=lifespan,
    )
    app.include_router(build_router(cfg))
    return app


def run() -> None:
    """CLI entry point: `yfinance-service`."""
    settings = Settings()
    uvicorn.run(
        "yfinance_service.main:create_app",
        factory=True,
        host="0.0.0.0",
        port=8001,
        log_config=None,  # we configure structlog ourselves
    )


if __name__ == "__main__":
    run()
