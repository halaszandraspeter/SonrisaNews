# Sonrisa News

> **Free, open-source alerts for the world — breaking news, market movements, natural disasters.**
> Delivered by email and Slack. Self-hostable. No cloud subscription.

Sonrisa News lets users subscribe to alerts about things that matter — RSS news feeds, market %-change events, and natural disasters — and get notified through channels they choose (email, Slack, and more to come).

## Status

🚧 **24-hour MVP in progress.** Planning complete; **wave 6 (news poller + matcher) is the latest shipped milestone**.
Work on this checklist was paused after wave 4; the wave 5 (alerts + filters) and wave 6 (news poller + matcher) work was picked up and shipped afterwards, with wave 5 shipped out of strict wave order. The wave table below reflects what actually shipped, not the original 24-hour timeline.

| Wave | Title | Status |
|---|---|---|
| 1 | Scaffold + plumbing | ✅ Shipped (2026-06-04) |
| 2 | Database + persistence skeleton | ✅ Shipped (2026-06-04) |
| 3 | Auth + RBAC (DB-driven) | ✅ Shipped (2026-06-05) |
| 4 | Channel abstraction | ✅ Shipped (2026-06-05) |
| 5 | Alert CRUD + filters | ✅ Shipped (2026-06-05, post-pause) |
| 6 | News poller + matcher | ✅ Shipped (2026-06-05) |
| 7 | yfinance sidecar + market poller | ⬜ Not started |
| 8 | Disaster poller + dispatcher + digests | ⬜ Not started |
| 9 | Onboarding wizard + dashboard | ⬜ Not started |
| 10 | Admin | ⬜ Not started |
| 11 | E2E + observability + polish | ⬜ Not started |
| 12 | Release prep | ⬜ Not started |

Detailed handoffs (what landed, what was deferred, and what's still open) live in [`docs/handoffs/`](docs/handoffs/):

- [`wave1-handoff.md`](docs/handoffs/wave1-handoff.md) — scaffold + the 12-file UI/backend review follow-up pass
- [`wave2-to-future.md`](docs/handoffs/wave2-to-future.md) — schema + 21 deferred items routed to future waves
- [`wave3-handoff.md`](docs/handoffs/wave3-handoff.md) — DB-driven RBAC (5 tables, 16 permissions, 26 grants)
- [`wave3-frontend-handoff.md`](docs/handoffs/wave3-frontend-handoff.md) — auth client, sign-in / sign-up pages, `AuthGate`
- [`wave4-handoff.md`](docs/handoffs/wave4-handoff.md) — `EmailChannel` + `SlackChannel`, 24h verification expiry, `Notification.DedupeKey`
- [`wave4-frontend-followup.md`](docs/handoffs/wave4-frontend-followup.md) — `AddChannelDialog` polish + 10 deferred NITs
- [`wave5-handoff.md`](docs/handoffs/wave5-handoff.md) — Alert CRUD + filters (backend, post-pause)
- [`wave5-frontend-handoff.md`](docs/handoffs/wave5-frontend-handoff.md) — alert + filter admin UI (post-pause)
- [`wave6-handoff.md`](docs/handoffs/wave6-handoff.md) — RSS `IDataSource` + matcher + 2-min poller (backend)
- [`wave6-frontend-handoff.md`](docs/handoffs/wave6-frontend-handoff.md) — `TestAlertDialog` + "Test this alert" button (4 review rounds, 23 NITs deferred to wave 11)

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
