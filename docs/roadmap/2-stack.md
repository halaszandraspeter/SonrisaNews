# Sonrisa News — Stack (MVP, 24-hour scope)

> **Constraints driving this stack** (from `1-features.md` and the brief):
> - Reliability is the number one priority. Industry-standard RBAC.
> - Free, no cloud subscription. Local development must be easy.
> - Pluggable components — the data-feeding layer must be swappable, and channels are an interface.
> - Public B2C. SEO matters on landing pages. Three data domains. Two channels (email, Slack) in MVP.
> - User-managed decisions live in `1-features.md`; this doc picks the **how**.
> - **Document status**: Proposal. User has final word.

---

## 1. High-level architecture

```
┌────────────────────────────────────────────────────────────────────┐
│  Browser (Web)                                                     │
│                                                                    │
│   Next.js 16 (App Router) + React 19 + TypeScript + MUI v9         │
│   React Query for client cache, Zod for forms                      │
└────────────────────┬───────────────────────────────────────────────┘
                     │  HTTPS / JSON (OpenAPI-generated types)
                     │  Cookie: refresh token (httpOnly, SameSite=Lax)
                     │  Header: Authorization: Bearer <jwt>
                     ▼
┌───────────────────────────────────────────────────────────────────┐
│  C# / .NET 10 backend (ASP.NET Core, Kestrel)                     │
│                                                                   │
│   ┌─────────────────┐  ┌─────────────────┐  ┌──────────────────┐  │
│   │ Api project     │  │ Worker project  │  │ Yfinance sidecar │  │
│   │ (HTTP)          │  │ (IHostedService │  │ client           │  │
│   │ - Controllers   │  │  background)    │  │ (delegating      │  │
│   │ - Middleware    │  │ - Pollers       │  │  handler)        │  │
│   │ - Auth/RBAC     │  │ - Matcher       │  └────────┬─────────┘  │
│   │ - Swagger UI    │  │ - Dispatcher    │           │            │
│   └────────┬────────┘  └────────┬────────┘           │            │
│            │                    │                    │            │
│            └────────┬───────────┘                    │            │
│                     │                                │            │
│   ┌─────────────────▼─────────────────┐              │            │
│   │  Shared library                   │              │            │
│   │  - Domain entities                │              │            │
│   │  - DbContext (EF Core)            │              │            │
│   │  - INotificationChannel           │              │            │
│   └────────┬──────────────┬───────────┘              │            │
│            │              │                          │            │
└────────────┼──────────────┼──────────────────────────┼────────────┘
             │              │                          │
             │              │  HTTP                    │  HTTP
             │              ▼                          ▼
             │      ┌──────────────┐           ┌──────────────┐
             │      │ SQLite (MVP) │           │ Python       │
             │      │ single .db   │           │ yfinance     │
             │      │ file         │           │ sidecar      │
             │      │ → Postgres   │           │ (FastAPI)    │
             │      │   in prod    │           │ uvicorn      │
             │      └──────────────┘           └──────────────┘
             │
             │  SMTP / Slack webhook
             ▼
       Email provider, Slack
```

**Why two backend projects (`Api` + `Worker`)**: keeps the public HTTP surface small and lets the worker process be scaled independently. They share the same `Domain` + `Infrastructure` libraries, so we don't duplicate logic. In the MVP both run in a single process via .NET Aspire's AppHost (see §9); in a future microservice split they become separate deployables without code changes.

**Why Python sidecar**: yfinance IS Python. Trying to call it from C# directly is fragile (undocumented endpoints, no SDK, breaks on Yahoo changes). A 50-line FastAPI service that exposes `GET /quote?symbol=AAPL` is the natural seam and lets us swap to a paid real-time provider later behind the same URL.

---

## 2. Frontend

| Decision | Choice | Why |
|---|---|---|
| Framework | **Next.js 16 (App Router)** | SEO out of the box (critical for B2C landing). Server Components reduce JS payload. Same language as the future mobile codebase if you add React Native later. |
| Language | **TypeScript strict** | Catches errors at build time, matches backend types via OpenAPI codegen. |
| UI library | **MUI v9** | Comprehensive, free, opinionated. Saves CSS time in 24h. v9 is significantly lighter than v6 (smaller bundle, fewer style-engine dependencies) and the current stable line — no reason to start on the older version. Tradeoff: opinionated styling can be hard to override deeply (acceptable for an MVP). |
| Data fetching | **TanStack Query (React Query) v5** | Server state caching, background re-fetch, optimistic updates. Pairs well with Next.js. |
| Forms | **React Hook Form + Zod** | Type-safe, minimal re-renders. |
| Auth client | **`fetch` with refresh interceptor** | No third-party SDK. Refresh token in httpOnly cookie; access token in memory. |
| OpenAPI client | **`openapi-typescript` + `openapi-fetch`** | Backend Swagger → TS types → typed fetch client. No drift. |
| Email rendering | **React Email** | Type-safe email templates; preview server in dev. |
| Tests | **Vitest** (unit), **Playwright** (e2e) | Standard. |
| Lint/format | **ESLint (typescript-eslint strict) + Prettier** | Standard. |

**Folder layout** (mirrors your "use the app → find the code" rule):

```
web/
├── app/                    ← Next.js App Router
│   ├── (marketing)/        ← public, no auth
│   │   ├── page.tsx        ← landing
│   │   ├── privacy/
│   │   └── terms/
│   ├── (app)/              ← authenticated
│   │   ├── layout.tsx      ← auth-gated shell
│   │   ├── alerts/
│   │   ├── settings/
│   │   └── onboarding/     ← the wizard
│   ├── (admin)/            ← admin role only
│   │   ├── layout.tsx
│   │   ├── sources/
│   │   ├── users/
│   │   ├── health/
│   │   └── announcements/
│   ├── api/                ← BFF routes (auth callbacks, etc.)
│   └── (auth)/             ← sign-in, sign-up, verify
├── components/             ← shared UI
├── features/               ← feature modules (alerts, channels, sources…)
├── lib/                    ← api client, query client, auth helpers
├── styles/
└── public/
```

**Why `features/` over `pages/`-mirroring**: Next.js already has `app/`. `features/` groups code by domain so cross-page logic (e.g. alert builder used on onboarding AND alerts page) lives in one place. The marketing/app/admin segments are route boundaries; the feature boundaries live inside.

---

## 3. Backend (C# / .NET)

| Decision | Choice | Why |
|---|---|---|
| Runtime | **.NET 10 LTS** (current LTS at build time) | Industry standard. Strong typing, async-native, mature observability, OpenAPI tools. **Roadmap: .NET 11** — anticipated async/await performance improvements; revisit after GA. |
| Web framework | **ASP.NET Core Minimal API + Carter (or controllers)** | Minimal API is lean; controllers are familiar. Pick controllers for the comfort factor. |
| ORM | **EF Core 10 + Pomelo MySQL provider for Postgres + Microsoft.EntityFrameworkCore.Sqlite for dev** | Migrations are first-class. `dotnet ef migrations` workflow. |
| Validation | **FluentValidation** | Industry standard. Cleaner than data-annotations. |
| Auth | **ASP.NET Core Identity + JWT bearer + custom RBAC layer** | Identity handles password hashing, lockout, tokens. JWT bearer middleware for stateless auth. RBAC is a custom policy layer on top. |
| RBAC | **Casbin.NET** | Industry-standard, declarative policy files, role + permission matrix, supports hierarchical roles. Alternative: hand-rolled `[Authorize(Roles=…)]` with a policy provider. Casbin wins for "industry standard" but is one more dep. |
| OpenAPI | **Swashbuckle** (or **Scalar** for the UI) | Generates OpenAPI 3.1 from controllers. UI for the docs. |
| Background work | **`IHostedService` + `BackgroundService`** | Built-in. We don't need Hangfire/Quartz for MVP — no scheduling beyond "every N minutes". |
| Email | **MailKit** (SMTP) | Free, well-maintained. Pluggable: can use local SMTP (MailHog in dev) or a real SMTP (Resend free tier). |
| Slack | **HttpClient + JSON** | Incoming webhooks are one POST. No SDK needed. |
| HTTP client for yfinance | **`HttpClient` + `Microsoft.Extensions.Http.Resilience` (Polly v8)** | Typed client, retries, timeouts. |
| Logging | **`Microsoft.Extensions.Logging` → Serilog → console JSON** | Structured. In prod, swap to OpenTelemetry exporter. |
| Caching | **`Microsoft.Extensions.Caching.Hybrid`** | L1 in-process + L2 (Redis) when we need it. In MVP, L1 only. |
| Tests | **xUnit + FluentAssertions** for unit/integration tests; SQLite in-memory for MVP integration. **No Testcontainers** in MVP (it pulls a Docker dependency, which is out of scope per resolved decision 5). | Industry standard. SQLite in-memory is enough for MVP; Testcontainers is a post-MVP option when CI needs a real Postgres. |
| Code quality | **EditorConfig + .NET format + Roslyn analyzers** | Built-in. |

**RBAC** (the part you specifically called out):

- Roles seeded from `appsettings.json` at startup: `User`, `Admin`, `System`.
- Permissions enumerated as constants in `Permissions.cs` (e.g. `Alerts.Read.Own`, `Alerts.Write.Own`, `Sources.Write.Any`, `Users.Suspend`, `AuditLog.Read`).
- Casbin policy file `rbac_policy.csv`:
  ```
  p, Admin, Sources.*, *
  p, Admin, Users.*, *
  p, User, Alerts.*, own
  p, User, Channels.*, own
  g, alice, Admin
  ```
- Endpoints decorated with `[Authorize(Policy = "Alerts.Write.Own")]`. Policy handler in `RbacPolicyHandler.cs` consults Casbin with `(subject=currentUser, action=permission, resource=targetEntity)`.
- Audit-logged on every admin action.

**Folder layout**:

```
backend/
├── SonrisaNews.sln
├── src/
│   ├── SonrisaNews.Api/         ← ASP.NET Core host
│   │   ├── Controllers/
│   │   ├── Middleware/          ← request logging, exception handler
│   │   ├── Auth/                ← JWT, RBAC policies
│   │   └── Program.cs
│   ├── SonrisaNews.Worker/      ← background services host
│   │   ├── Pollers/             ← NewsPoller, DisasterPoller, MarketPoller
│   │   ├── Matcher/
│   │   ├── Dispatcher/
│   │   └── Program.cs
│   ├── SonrisaNews.Domain/      ← entities, value objects, enums
│   ├── SonrisaNews.Infrastructure/
│   │   ├── Persistence/         ← DbContext, migrations, repositories
│   │   ├── Channels/            ← INotificationChannel, EmailChannel, SlackChannel
│   │   ├── Sources/             ← RssFetcher, UsgsClient, GdacsClient, NhcClient
│   │   ├── Ai/                  ← OnboardingAiService interface
│   │   └── Yfinance/            ← YfinanceClient (HttpClient)
│   └── SonrisaNews.Shared/      ← cross-cutting: errors, options, helpers
│   └── SonrisaNews.AppHost/     ← .NET Aspire AppHost (dev process orchestrator)
├── tests/
│   ├── SonrisaNews.UnitTests/
│   ├── SonrisaNews.IntegrationTests/   ← WebApplicationFactory + SQLite
│   └── SonrisaNews.E2ETests/           ← Playwright
└── deploy/                        ← post-MVP: systemd units, Windows Service installer
```

**Why Api + Worker are separate projects (not one process with two roles)**: a single deploy unit is simpler in MVP, but splitting them lets us (a) scale workers independently, (b) put workers on cheaper compute, (c) give them different network egress rules. The cost is two `Program.cs` files and a shared library. The .NET Aspire AppHost (§9) runs both in a single dev process for the MVP; the **microservice migration path is one publisher call away** (`AddDockerComposePublisher` or `AddKubernetesPublisher`) when we're ready to split.

---

## 4. Python yfinance sidecar

| Decision | Choice | Why |
|---|---|---|
| Framework | **FastAPI** | Async, type-hinted, auto-OpenAPI. |
| Server | **uvicorn** | Standard. |
| Quotes | **yfinance** | Free, 15-min delayed. No API key. |
| Endpoint | `GET /quote?symbol=AAPL` → `{ symbol, price, change_pct, volume, as_of }` | One endpoint covers all market alert needs. |
| Endpoint | `GET /quotes?symbols=AAPL,MSFT` → batched | Avoid per-symbol HTTP round-trip. |
| Caching | **In-process TTL 60s** | Quotes don't change that fast; this protects against alert storms. |
| Tests | **pytest + httpx.AsyncClient** | Standard. |
| Image | **`python:3.12-slim`** | Small, fast cold start. |

**C# side**: `YfinanceClient` is a typed `HttpClient` with the standard retry/timeout policies. If the sidecar is down, the worker logs a warning, the market matcher skips this round, and the admin health page flags it. Reliability principle: degrade gracefully, never crash the worker.

**When we move to a paid real-time provider** (e.g. Polygon), we keep the same `YfinanceClient` interface and swap the implementation. The worker doesn't know the difference.

---

## 5. Data

| Decision | Choice | Why |
|---|---|---|
| MVP DB | **SQLite** (single file at `data/sonrisa.db`) | Zero install. Prisma-equivalent EF Core experience. Trivial backup. Perfect for solo dev + 24h MVP. |
| Prod DB | **Postgres 16** (on the same host) | When the user count grows past a few hundred active users and the matcher can't keep up, or when we need full-text search on event payloads. The schema is Postgres-compatible from day 1 (no SQLite-specific types). Postgres is installed natively (no Docker), and we ship a `systemd` / Windows Service unit file alongside the .NET worker. |
| Migration tool | **EF Core migrations** | Standard. Migrations are forward-only; Postgres swap is `provider switch + one-line connection string`. |
| Connection | EF Core, **scoped per request** in Api, **singleton DbContext** (carefully) or scoped in Worker | Standard patterns. |
| Full-text search | MVP: `LIKE %?%` with a FTS index later (Postgres `tsvector` or SQLite FTS5) | Not in MVP. |

**Why SQLite is fine for an MVP alerts service**:
- A few thousand events per day is trivial for SQLite.
- A few thousand users with simple alert rules is fine.
- Single-writer is not a problem because we already serialize work via background polls.
- The swap to Postgres is a connection-string change. EF Core handles it.

**The line where we MUST migrate**:
- Concurrent writes to the same row (e.g. dispatching notifications to many channels at once).
- Full-text search on event payloads.
- Multi-instance worker (horizontal scale).
- Disk > 100 GB of events retained (SQLite is fine for hundreds of MB).

---

## 6. Pluggability — the things you specifically asked for

### 6.1 Channel abstraction

```csharp
public interface INotificationChannel
{
    string Type { get; }                                    // "email" | "slack" | ...
    Task<VerificationChallenge> StartVerificationAsync(string destination, CancellationToken ct);
    Task<bool> VerifyAsync(string destination, string code, CancellationToken ct);
    Task<SendResult> SendAsync(NotificationPayload payload, CancellationToken ct);
}
```

Registered in DI:
```csharp
services.AddKeyedScoped<INotificationChannel, EmailChannel>("email");
services.AddKeyedScoped<INotificationChannel, SlackChannel>("slack");
// services.AddKeyedScoped<INotificationChannel, SmsChannel>("sms");     // future
// services.AddKeyedScoped<INotificationChannel, WebhookChannel>("webhook");
```

The dispatcher resolves the right channel via the keyed service. Adding a new channel in the future = one new class + one DI line. No controller change. No DB schema change (the `Channel.Type` column is a string).

### 6.2 Data source abstraction

```csharp
public interface IDataSource
{
    string Id { get; }
    SourceType Type { get; }   // News | MarketSymbol | Disaster
    TimeSpan PollInterval { get; }
    Task<IReadOnlyList<RawEvent>> FetchAsync(CancellationToken ct);
}
```

Each source is a class (`RssSource`, `UsgsEarthquakeSource`, `GdacsSource`, `NhcHurricaneSource`, plus one per enabled market symbol in admin). The worker iterates registered sources on a scheduler. Adding a new source = implement `IDataSource` + register in DI.

The **admin** manages the list of enabled sources in the DB. The **engine** doesn't care which sources exist — it just polls whatever is registered and enabled.

### 6.3 AI service abstraction

```csharp
public interface IOnboardingAiService
{
    Task<AlertSuggestions> SuggestAlertsFromTextAsync(string freeText, CancellationToken ct);
    Task<AlertSuggestions> SuggestAlertsFromAnswersAsync(OnboardingAnswers answers, CancellationToken ct);
}
```

MVP implementation: rule-based (no LLM). If we add an LLM later, it's the same interface, swappable via DI. This is the "reliable for the user, flexible for us" pattern you wanted.

### 6.4 Configuration

- All secrets via environment variables (12-factor).
- `appsettings.json` for non-secrets; `appsettings.Development.json` for local overrides; `appsettings.Production.json` for prod defaults.
- In dev, secrets can come from `dotnet user-secrets`. In prod, from env vars (no keyring needed for an MVP).
- A `docker-compose.yml` is **not** part of the MVP. When packaging for deployment later, we generate one from the Aspire AppHost via `AddDockerComposePublisher` — but that is a post-MVP step.

---

## 7. Email & Slack delivery

- **Email**: MailKit → SMTP. Local dev: MailHog running natively as a Go binary (see §9.2). Prod: user's choice — Resend free tier (3k/month), or any SMTP. The SMTP host/user/pass/port come from config.
- **Slack**: HttpClient → Slack incoming webhook URL. The user pastes the URL, we send a test message to verify.
- **Email templates**: React Email (TS), rendered to HTML + plain text. Versioned in the repo, no UI editor in MVP.

---

## 8. Auth flow (proposed)

1. **Sign-up**: POST `/auth/signup` → creates user (unverified) → sends email with `/auth/verify?token=…`.
2. **Verify**: GET `/auth/verify?token=…` → marks user verified → issues access + refresh.
3. **Sign-in**: POST `/auth/signin` → returns access JWT (15m) + sets refresh cookie (httpOnly, Secure, SameSite=Lax, 30d).
4. **Refresh**: POST `/auth/refresh` (cookie required) → new access JWT. The client refreshes silently on 401.
5. **Sign-out**: POST `/auth/signout` → clears refresh cookie, revokes refresh in DB.
6. **Password reset**: POST `/auth/forgot` → email with `/auth/reset?token=…` → POST `/auth/reset` with new password.

JWT payload: `sub`, `email`, `role`, `iat`, `exp`, `jti`. Secret is a 256-bit env var. RS256 (asymmetric) so the worker (which only verifies, never signs) doesn't need the signing key. Refresh tokens are opaque, stored in `RefreshToken` table with `revoked_at` and `replaced_by_id` for rotation.

**RBAC** (Casbin) is consulted **after** authentication, on every controller action. The policy file is version-controlled.

---

## 9. Local development

> **No Docker, no containers.** Per resolved decision 5: the customer may not have a Docker license (or any container runtime). Everything in the dev experience is **native processes on the host OS**. Migration to containers later is a packaging decision, not an architecture decision.

### 9.1 Process model — managed by **.NET Aspire** (AppHost)

Aspire is the .NET team's opinionated stack for running multi-service apps in development. It is **not** a container tool: the default orchestrator runs each service as a native process on the host. A future switch to Kubernetes/Compose is one `AddDockerComposePublisher` / `AddKubernetesPublisher` call — the source code doesn't change.

We add an `AppHost` project to the solution:

```
backend/
└── SonrisaNews.AppHost/        ← Aspire AppHost
    ├── Program.cs
    └── SonrisaNews.AppHost.csproj   ← references all services
```

`AppHost/Program.cs` (sketch):

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Native process services
var api   = builder.AddProject<Projects.SonrisaNews_Api>("api");
var work  = builder.AddProject<Projects.SonrisaNews_Worker>("worker");

// External / non-managed resources (Aspire's `AddExternalService`)
var maildev = builder.AddExternalService("maildev", "smtp://localhost:1025")
                     .WithHttpEndpoint(name: "ui", targetPort: 8025);
var yfin    = builder.AddExternalService("yfinance", "http://localhost:8001");

// SQLite attached as a connection string resource
var db = builder.AddConnectionString("sonrisa", "Data Source=data/sonrisa.db");

// Wire references
api.WithReference(db).WithReference(maildev).WithReference(yfin);
work.WithReference(db).WithReference(yfin);

builder.Build().Run();
```

What this gets us **for free**, with zero container dependency:
- A single `dotnet run --project backend/SonrisaNews.AppHost` boots Api + Worker + (in production-grade setups) the yfinance sidecar.
- The Aspire **dashboard** at `localhost:15000` gives us logs, traces, and resource health. This **replaces the need for us to build the admin observability page from scratch** — and we still ship our own health page because the dashboard is dev-only.
- Service discovery via `WithReference` — Api's config gets `ConnectionStrings:sonrisa`, `Services:maildev:smtp:0`, etc. injected automatically.
- One-command start, one-command stop, structured logs.
- **Future-microservice path** (per resolved decision 4): the same AppHost can `AddContainer` for individual services when we split, and ultimately `AddKubernetesPublisher` deploys the whole graph to a cluster with zero code changes.

### 9.2 Prerequisites (what the user installs — no Docker)

- **.NET 10 SDK** (includes Aspire workload: `dotnet workload install aspire`).
- **Node 22 LTS** + **pnpm** (or npm — we standardize on pnpm).
- **Python 3.12** + **uv** (for the yfinance sidecar).
- **MailHog binary** (Go binary — no Docker required). Available as a standalone executable:
  - macOS: `brew install mailhog`
  - Linux: download from `github.com/mailhog/MailHog/releases` → `MailHog` binary.
  - Windows: download `MailHog_windows_amd64.exe` and add to PATH.
  - This is a Go program that runs natively. It listens on `:1025` (SMTP) and `:8025` (web UI).
- **SQLite** — built into the OS / .NET runtime, no install.
- **(Postgres later, not now)** — when we migrate, we ship a Windows Service / Linux systemd unit file, not a container.

### 9.3 One-command start

```bash
# from repo root
./scripts/dev.sh        # macOS / Linux
./scripts/dev.ps1       # Windows PowerShell
```

The script:
1. Verifies prerequisites (`.NET`, `node`, `pnpm`, `python`, `uv`, `mailhog`).
2. Starts MailHog natively in the background: `mailhog > /tmp/mailhog.log 2>&1 &` (or Windows equivalent).
3. Starts the yfinance sidecar natively: `uv run --project services/yfinance uvicorn yfinance_service.main:app --port 8001 &`.
4. Runs EF Core migrations: `dotnet ef database update --project backend/src/SonrisaNews.Infrastructure`.
5. Starts the .NET AppHost (which boots Api + Worker with service discovery wired up).
6. Starts the Next.js dev server: `pnpm --dir web dev`.
7. Prints the URLs: app at `http://localhost:3000`, Aspire dashboard at `http://localhost:15000`, MailHog UI at `http://localhost:8025`, API at `http://localhost:5080`.

The script is **idempotent** — running it twice is safe. It kills any stale process from a previous run before starting a new one.

### 9.4 When the user moves to Postgres (or any other change)

Two cases:
- **Schema or config change**: edit `appsettings.Development.json` / `appsettings.json` and re-run the script. The AppHost re-reads.
- **Database change** (SQLite → Postgres): we change the EF Core provider in `Infrastructure/Persistence`, add a `Postgres` connection string option, and document the manual `psql` steps in the README. No container is required — Postgres has native installers for Windows, macOS, and Linux.

### 9.5 What "easy setup" means in practice

- Total time from clean clone to first alert firing: **< 10 minutes**.
- No cloud account, no container runtime, no license keys.
- One terminal, one command.
- Clear error messages if a prerequisite is missing (the script checks).

---

## 10. Testing

| Layer | Tool | Coverage target (MVP) |
|---|---|---|
| Backend unit | **xUnit + FluentAssertions** | 70% on `Domain` and `Channels` |
| Backend integration | **xUnit + WebApplicationFactory + SQLite in-memory** | All controllers happy path + 1 error path |
| Frontend unit | **Vitest + Testing Library** | Hooks, formatters, reducers |
| Frontend e2e | **Playwright** | Sign-up → onboarding → create alert → receive email (MailHog) happy path |
| Load smoke | **`dotnet run` + a simple k6 script** | 100 events/s sustained for 5 min |
| Python sidecar | **pytest + httpx.AsyncClient** | Endpoint contracts |

CI is **GitHub Actions** (free for public repos). Workflow: `lint → unit → integration → e2e → build`. Runs on PR and main. For an MVP we keep it simple: one workflow file, parallel jobs. No `docker build` / `docker push` step in MVP — that's a packaging decision we defer.

---

## 11. Repo layout (monorepo, single `git`)

```
sonrisanews/
├── docs/
│   └── roadmap/
│       ├── 1-features.md
│       ├── 2-stack.md            ← this file
│       └── 3-ai-environment.md
├── web/                          ← Next.js
├── backend/                      ← .NET
├── services/
│   └── yfinance/                 ← Python sidecar
├── deploy/
│   ├── systemd/                  ← Linux service units (post-MVP)
│   ├── windows-service/          ← Windows service installer (post-MVP)
│   └── .env.example
├── scripts/
│   ├── dev.sh
│   └── dev.ps1
├── .github/
│   ├── workflows/
│   └── copilot-instructions.md
├── AGENTS.md
└── README.md
```

Polyrepo is an option. Monorepo is chosen because: one PR touches frontend + backend + (sometimes) sidecar; one CI run tests the whole thing; one `git clone` for the user.

---

## 12. Risks and mitigations (24-hour MVP)

| Risk | Likelihood | Mitigation |
|---|---|---|
| yfinance changes its scraping surface and breaks the sidecar | Medium | The sidecar is one tiny service. We test on a known symbol at startup. If it fails, market alerts pause and admin sees a banner. We have a fallback: hardcode the last known quote for 24h. |
| SQLite hits a single-writer bottleneck under alert storms | Low for MVP | Digest mode reduces per-match dispatch. Real-time path is batched. |
| Email deliverability in dev (we use MailHog) doesn't match prod | N/A — dev only | Real provider is configured via env vars. We never claim "sent" in tests. |
| 24h scope is tight — feature creep | High | `1-features.md` says "out of MVP". Stick to it. |
| MUI customization eats time | Medium | MUI defaults only. No theme overrides in MVP. |
| RBAC over-engineering in 24h | Medium | Use Casbin. If too heavy, fall back to a hand-rolled `[Authorize(Policy=…)]` provider. Either works; the policy file is what matters. |

---

## 13. Decisions that need your sign-off before we lock this

**Resolved** (locked in):
- **.NET version**: 10 LTS for MVP; .NET 11 considered post-MVP for async/perf.
- **RBAC**: Casbin.NET (industry-standard).
- **UI**: MUI **v9** (lighter than v6, current stable).
- **Process model**: combined Api+Worker in MVP via .NET Aspire AppHost; microservice split is a future packaging step (same `AddContainer` / `AddKubernetesPublisher` extension, no source changes).
- **No Docker / no containers** in MVP. All processes are native. MailHog runs as a Go binary, Postgres installed natively, packaged as `systemd` unit / Windows Service later. AppHost can later publish to Docker Compose / Kubernetes if the deployment target demands it.
- **Time format**: all timestamps stored as UTC, rendered in the user's local timezone.

No open questions remain in this doc. If you sign off, we move on to `3-ai-environment.md`.
