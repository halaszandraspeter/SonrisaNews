# yfinance sidecar

FastAPI service that exposes `yfinance` quotes behind a typed HTTP API. The Sonrisa News .NET worker calls this sidecar every 5 minutes (configurable) for every enabled market symbol.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/health` | Liveness probe — always 200 if the process is up. |
| `GET` | `/ready`  | Readiness probe — runs a tiny yfinance call to confirm the upstream is reachable. |
| `GET` | `/quote?symbol=AAPL` | Single-symbol quote. |
| `GET` | `/quotes?symbols=AAPL,MSFT,GOOG` | Batched quotes. Returns a per-symbol map; bad symbols get a `null` value with a structured error. |

## Run locally

```bash
# from the repo root
./scripts/dev.sh     # macOS / Linux
# or
.\scripts\dev.ps1    # Windows PowerShell
```

The dev script starts MailHog, this sidecar (on `:8001`), the .NET AppHost, and the Next.js dev server.

## Run standalone

```bash
cd services/yfinance
uv sync
uv run uvicorn yfinance_service.main:app --port 8001
```

## Tests

```bash
cd services/yfinance
uv sync
uv run pytest
```

Tests are hermetic — yfinance is mocked at the import boundary; no live network.

## Configuration

All config via env vars (parsed by `Settings` in `settings.py`):

| Env var | Default | Meaning |
|---|---|---|
| `YF__BASE_URL` | `http://localhost:8001` | Self-URL, used in OpenAPI docs |
| `YF__CACHE_TTL_SECONDS` | `60` | In-process quote cache TTL |
| `YF__UPSTREAM_TIMEOUT_SECONDS` | `10` | Per-quote timeout against yfinance |
