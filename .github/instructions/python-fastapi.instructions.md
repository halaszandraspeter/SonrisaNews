---
description: 'Python / FastAPI coding standards for the yfinance sidecar.'
applyTo: 'services/**/*.py'
---

# Python / FastAPI — Sonrisa News (yfinance sidecar)

> **Read first**: [AGENTS.md](../../../AGENTS.md), [docs/roadmap/2-stack.md](../../../docs/roadmap/2-stack.md) §4.

## Stack

- Python 3.12. FastAPI. uvicorn. yfinance.
- Dependency management with `uv` (or `poetry` if `uv` is unavailable). Lockfile is committed.
- Tests with `pytest` + `httpx.AsyncClient`. No live network in tests.

## Async discipline

- **`async`/`await` everywhere.** No blocking I/O on the event loop. `time.sleep`, `requests`, `open(..., 'r').read()` — all forbidden in async paths.
- **`httpx.AsyncClient` is injected**, never constructed per call. One shared client per app, opened at startup, closed at shutdown (FastAPI `lifespan`).
- **`yfinance` is the odd one out** — its API is sync. Wrap calls in `asyncio.to_thread(...)` or run them in a thread pool executor with a bounded concurrency limit. Don't `await yf.Ticker(...).history(...)` directly.
- **All I/O has a timeout.** `httpx.AsyncClient(timeout=…)`, and the worker side has a hard ceiling (e.g. 10s per quote, 60s per batch).

## Endpoints

- **One endpoint per operation.** Don't overload paths with magic query params.
- **Pydantic v2 models** for request and response. One model per shape. Reuse via `model_dump()`.
- **OpenAPI doc is auto-generated.** Add `summary=`, `description=`, `response_model=`, `responses={...}` decorators. Document the 4xx cases (404: symbol not found, 503: upstream down, 429: rate limited).
- **No business logic in the route function.** Routes parse, validate, call a service, return the result. The service does the work.
- **Errors are typed.** Use `HTTPException(status_code=..., detail={"code": "...", "message": "..."})`. Don't return a `JSONResponse` directly.

## Logging

- `structlog` configured to emit JSON in prod, pretty in dev. No `print()` in service code.
- **Structured properties, not f-strings**: `log.info("quote_fetched", symbol=symbol, latency_ms=latency)`, not `log.info(f"Fetched {symbol} in {latency}ms")`.
- **Never log a full payload** — log the `symbol` and a short summary. Quote data is public, but logs grow.

## Caching

- The `GET /quote?symbol=...` endpoint caches results in-process for 60s (configurable). Use a `cachetools` `TTLCache` or a simple dict with timestamps.
- **Cache key includes all input parameters**, not just the symbol. (`symbol=AAPL&force=False` ≠ `symbol=AAPL&force=True`.)
- **Cache misses are explicit.** Log them. A spike in misses is a signal the upstream is slow.

## Configuration

- All config from env vars, parsed by a `Settings` class (pydantic-settings).
- **No hard-coded URLs, ports, or secrets** in code.
- **No `os.environ.get(...)`** scattered through the code — centralize in `Settings`.

## Reliability

- **yfinance can fail.** Wrap every `yfinance` call in `try/except`, return a typed error (`UpstreamUnavailable`), never let the exception escape to the route.
- **One bad symbol must not poison a batch.** `GET /quotes?symbols=AAPL,INVALID,MSFT` returns `{"AAPL": {...}, "INVALID": null, "MSFT": {...}}` with a per-symbol status. The HTTP status is 200; the per-symbol `null` carries the error.
- **The sidecar starts even if yfinance is down.** Readiness probe is a separate `/health` that does a tiny quote check.

## Style

- Type hints **everywhere**. `def quote(symbol: str) -> Quote:` — not just `def quote(symbol):`.
- `from __future__ import annotations` at the top of every file.
- `pathlib.Path` over `os.path`.
- **`if TYPE_CHECKING:`** for typing-only imports (don't pay the import cost at runtime).
- File size: ~300 lines max. Extract when approaching the limit.

## Tests

- `pytest` + `pytest-asyncio`. `httpx.AsyncClient` against the FastAPI app.
- **No live network.** `yfinance` is mocked at the import boundary (use `monkeypatch` or a `conftest.py` fixture that returns canned data).
- **No live time.** Inject a clock; tests use a fake.
- **No `time.sleep`** in tests. Use `await asyncio.sleep(0)` to yield.

## Forbidden patterns (the agent must not introduce these)

- `print()` in service code (use `log.info(...)` instead)
- `requests` (use `httpx.AsyncClient`)
- `time.sleep` in async code
- Hard-coded URLs / ports / secrets
- `yfinance` calls outside `services/yfinance/` (no one else in the repo should import it)
- Returning untyped dicts (always use Pydantic models)
- Bare `except:` or `except Exception:` (always narrow)
- `from yfinance import *` (always import specific symbols)
- Untyped function signatures
