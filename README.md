# Sonrisa News

> **Free, open-source alerts for the world — breaking news, market movements, natural disasters.**
> Delivered by email and Slack. Self-hostable. No cloud subscription.

Sonrisa News lets users subscribe to alerts about things that matter — RSS news feeds, market %-change events, and natural disasters — and get notified through channels they choose (email, Slack, and more to come).

## Status

🚧 **24-hour MVP in progress.** Planning complete; bootstrap is done; **wave 2 (database + persistence skeleton) is the latest shipped milestone**.

| Wave | Title | Status |
|---|---|---|
| 1 | Scaffold + plumbing | ✅ Shipped (bootstrap) |
| 2 | Database + persistence skeleton | ✅ Shipped <!-- done: 2026-06-04, see PR pending --> |
| 3 | Auth + RBAC | ⬜ Not started |
| 4 | Channel abstraction | ⬜ Not started |
| 5 | Alert CRUD + filters | ⬜ Not started |
| 6 | News poller + matcher | ⬜ Not started |
| 7 | yfinance sidecar + market poller | ⬜ Not started |
| 8 | Disaster poller + dispatcher + digests | ⬜ Not started |
| 9 | Onboarding wizard + dashboard | ⬜ Not started |
| 10 | Admin | ⬜ Not started |
| 11 | E2E + observability + polish | ⬜ Not started |
| 12 | Release prep | ⬜ Not started |

The locked planning documents live in [`docs/roadmap/`](docs/roadmap/):

- [`1-features.md`](docs/roadmap/1-features.md) — what we're building
- [`2-stack.md`](docs/roadmap/2-stack.md) — how we're building it
- [`3-ai-environment.md`](docs/roadmap/3-ai-environment.md) — the AI harness that builds it

## Quick start

```bash
# macOS / Linux
./scripts/dev.sh

# Windows PowerShell
.\scripts\dev.ps1
```

That single command boots the full dev stack:

| Component | URL |
|---|---|
| App (Next.js) | http://localhost:3000 |
| API (.NET) | http://localhost:5080 |
| Scalar (API docs) | http://localhost:5080/scalar/v1 |
| Aspire dashboard | http://localhost:15000 |
| MailHog (dev email) | http://localhost:8025 |
| yfinance sidecar | http://localhost:8001/docs |

### Prerequisites

- **.NET 10 SDK** (with Aspire workload: `dotnet workload install aspire`)
- **Node 22 LTS** + **pnpm**
- **Python 3.12** + **uv**
- **MailHog** (Go binary, native install — no Docker)

See [`docs/roadmap/2-stack.md` §9.2](docs/roadmap/2-stack.md) for installation details per OS.

### Bootstrap admin

Set these in `.env` (copy from [`.env.example`](.env.example)):

```
SEED_ADMIN_EMAIL=admin@sonrisa.local
SEED_ADMIN_PASSWORD=replace-me-with-a-strong-password
```

The API creates this user on first startup. The user is forced to change their password on first sign-in.

## Repo layout

```
sonrisanews/
├── AGENTS.md                      ← project-wide AI ground rules
├── .github/
│   ├── copilot-instructions.md    ← Copilot-specific hooks
│   ├── instructions/              ← 8 per-domain instruction files
│   ├── agents/                    ← 7 custom agent personas
│   ├── skills/                    ← 5 project-specific skills
│   ├── hooks/                     ← Copilot hook config
│   └── workflows/ci.yml           ← GitHub Actions
├── .githooks/                     ← Git pre-commit hook
├── .vscode/
│   ├── tasks.json                 ← VS Code dev tasks
│   └── launch.json                ← F5 launch configurations
├── docs/roadmap/                  ← The 3 planning docs
├── scripts/
│   ├── dev.sh                     ← One-command start (macOS/Linux)
│   └── dev.ps1                    ← One-command start (Windows)
├── web/                           ← Next.js 16 (App Router) + MUI v9
├── backend/                       ← .NET 10 (Api + Worker + AppHost)
├── services/yfinance/             ← Python FastAPI sidecar
└── deploy/                        ← systemd / Windows Service (post-MVP)
```

## License

This project is open source. See [LICENSE](LICENSE) (TBD — MIT, to be added before first release).
