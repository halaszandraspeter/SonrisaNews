"""Structured logging setup via structlog."""

from __future__ import annotations

import logging
import sys

import structlog


def configure_logging(environment: str) -> None:
    """Wire structlog into the standard library logging.

    - In Development: pretty console output.
    - In any other environment (Production, Staging): single-line JSON to stdout.
    """
    is_dev = environment.lower() == "development"

    processors: list = [
        structlog.contextvars.merge_contextvars,
        structlog.processors.add_log_level,
        structlog.processors.TimeStamper(fmt="iso", utc=True),
        structlog.processors.StackInfoRenderer(),
        structlog.processors.format_exc_info,
    ]
    if is_dev:
        processors.append(structlog.dev.ConsoleRenderer(colors=True))
    else:
        processors.append(structlog.processors.JSONRenderer())

    structlog.configure(
        processors=processors,
        wrapper_class=structlog.make_filtering_bound_logger(logging.INFO),
        logger_factory=structlog.PrintLoggerFactory(file=sys.stdout),
        cache_logger_on_first_use=True,
    )
