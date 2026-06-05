# Sonrisa News — AI Environment (harness for safe, reliable, efficient dev)

> **Constraints driving this** (from prior docs + your memory `workflow.md`, `code-principles.md`, `mistakes-to-avoid.md`):
> - Reliability is the number one priority.
> - TDD-friendly stack. Strongly typed everywhere.
> - 24-hour MVP. We can only afford configuration that pays for itself.
> - The agent(s) must be safe (no rogue DB drops, no unscoped deploys, no hallucinated APIs).
> - The user has final word; the harness makes that easy to exercise.
> - **Document status**: Proposal. User has final word.

This document has three parts:
1. **The harness** — what files we add to the repo, what they contain, and why.
2. **The workflow** — how you and the agent use those files day-to-day.
3. **The safety nets** — what we *don't* let the agent do, and how we detect drift.

---

## 1. The harness (what we add to the repo)

All files below go to the repo root unless noted. Paths are relative to `sonrisanews/`.

### 1.1 Top-level project instructions

#### `AGENTS.md` (project-wide ground rules)

Loaded by **all** agents (Copilot CLI, VS Code chat, Copilot coding agent). Mirrors your existing `workflow.md` style but project-specific.

Contents (outline):
- **Mission**: build Sonrisa News, the alert system described in `docs/roadmap/1-features.md`. Reliability first.
- **Repo map**: which folder holds what (web/backend/services/deploy). One-line summaries.
- **Non-negotiables**: no secrets in code, no cloud subscription, no UI library other than MUI, no paid data providers, no .NET version drift, all tests must run in CI before merge.
- **PR rules**: every PR includes a one-line "what changed" + "how verified" + "rollback plan". Every PR is small enough to review in 5 minutes.
- **When in doubt**: ask the user. The user has the final word.

#### `.github/copilot-instructions.md` (Copilot Chat / coding agent)

Same content as `AGENTS.md` but with a few Copilot-specific hooks:
- Always `dotnet test` and `pnpm test` before claiming a backend or frontend change is done.
- Always `dotnet ef migrations add …` and commit the migration when changing the schema.
- Never edit `appsettings.Production.json` directly — propose the change in the PR description and let the user apply.

### 1.2 Per-domain instructions (auto-applied by file pattern)

These are `.instructions.md` files. They activate only when the agent touches matching files. We curate only what we'd lose sleep over.

| File | `applyTo` pattern | Purpose |
|---|---|---|
| `.github/instructions/csharp-dotnet.instructions.md` | `backend/**/*.cs` | C# coding standards, async rules, EF Core migration safety, RBAC checks, "always pass CancellationToken", "never `async void`". |
| `.github/instructions/nextjs-react.instructions.md` | `web/**/*.{ts,tsx}` | React/Next.js rules, "no client components in server-only trees", Zod for forms, React Query patterns, MUI conventions, no inline `style={}` when a `sx` prop will do. |
| `.github/instructions/python-fastapi.instructions.md` | `services/**/*.py` | yfinance sidecar rules, async/await discipline, structured logging, dependency-injected HTTP clients, no blocking I/O on the event loop. |
| `.github/instructions/database-migrations.instructions.md` | `backend/**/Migrations/**` | Migrations are forward-only, never edited after merge, always include a Down-script comment for reference. |
| `.github/instructions/rbac-policies.instructions.md` | `backend/**/Auth/**` | RBAC is the security boundary (DB-driven, 5 tables: `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`). Any change must include a unit test demonstrating that the new permission works AND a test demonstrating that an unauthorized user is rejected. Validation is by permission, never by role. |
| `.github/instructions/secrets.instructions.md` | `**/.env*`, `**/appsettings.*.json`, `**/secrets.*` | No real secrets. `.env` is gitignored. Use `dotnet user-secrets` for local dev. Production secrets come from env vars. If the agent sees what looks like a real key, stop and tell the user. |
| `.github/instructions/openapi-schema.instructions.md` | `backend/**/Controllers/**` | Every controller method must declare `[ProducesResponseType]`, `[ProducesResponseType(StatusCodes.Status4xx)]`, summary in XML doc. The OpenAPI doc is the contract with the frontend. |
| `.github/instructions/testing.instructions.md` | `**/*Tests*/**/*.{cs,ts}` | No tests that depend on real network. No tests that depend on real time without `IClock`. All tests must be hermetic and run on CI. |

The exact contents of each `.instructions.md` are short (30–80 lines), reference your memory files, and link to canonical docs. They are not the place for a full style guide — they are reminders and tripwires.

### 1.3 Custom agents (named, scoped personas)

These live in `.github/agents/`. Each agent is a persona with a narrow mandate.

| Agent | File | Mandate | Tools it can use |
|---|---|---|---|
| **TDD C# Implementer** | `tdd-csharp-implementer.agent.md` | Implements a backend feature in C# following TDD: red test → green code → refactor. Never reviews its own work. | read/write/edit, `dotnet test`, `dotnet build`, `dotnet ef migrations add` |
| **TDD Next.js Implementer** | `tdd-nextjs-implementer.agent.md` | Same, for the Next.js frontend. | read/write/edit, `pnpm test`, `pnpm build`, `pnpm lint` |
| **Backend Reviewer** | `backend-reviewer.agent.md` | Reads a diff, checks: security, RBAC, async safety, error handling, tests, OpenAPI annotations. Outputs a structured review. | read-only |
| **Frontend Reviewer** | `frontend-reviewer.agent.md` | Reads a diff, checks: a11y, MUI usage, server/client boundary, RSC pitfalls, error boundaries, OpenAPI client drift. | read-only |
| **Stack Doc Researcher** | `stack-doc-researcher.agent.md` | When the user asks "what's the best way to X in C# 10 / Next.js 16 / FastAPI", fetches canonical docs. Never invents APIs. | fetch_webpage, github_repo, read-only |
| **Triage** | `triage.agent.md` | First responder on a bug report or a failed CI run. Reproduces, isolates, and hands off to an implementer with a tight scope. | run_in_terminal, read, write (scoped) |
| **Roadmap Sync** | `roadmap-sync.agent.md` | When a feature lands, opens a PR that updates `1-features.md` to flip the status. (User confirms via the `/update-docs` prompt — your existing rule.) | read/write, github PR |

Two TDD implementer personas match the TDD-priority signal from the stack decision. The reviewers are split so a backend change never gets reviewed by someone who can only think in React.

### 1.4 Skills (self-contained, on-demand)

Skills are like mini-instructions that include bundled assets (prompts, scripts, snippets). We install a small, curated set from `github/awesome-copilot` and write 3 of our own.

**Installed from awesome-copilot** (via the marketplace — your earlier instruction):

| Skill | Why we install it |
|---|---|
| `csharp-dotnet-development` plugin (skills: `csharp-async`, `aspnet-minimal-api-openapi`, `csharp-xunit`, `dotnet-best-practices`) | C# is our backend. These are the canonical patterns. |
| `expert-nextjs-developer` agent (we treat it as a skill we can invoke, then customize) | Our frontend framework. |
| `openapi-to-application-csharp-dotnet` plugin | When we add a new endpoint, this gives us a starter that matches the OpenAPI doc — keeps the contract honest. |
| `openapi-to-application-python-fastapi` plugin | Same for the sidecar. |
| `frontend-web-dev` plugin (TS, React, CSS) | General web quality. |
| `security-review` skill | We run this against any RBAC or auth PR. |
| `acquire-codebase-knowledge` skill | First thing we run in a fresh clone so the implementer agents know the layout. |

**Written by us (project-specific)**:

| Skill | Folder | Contents |
|---|---|---|
| `add-a-channel` | `.github/skills/add-a-channel/` | SKILL.md: step-by-step checklist for adding a new `INotificationChannel`. Includes a template `EmailChannel.cs` to copy from. Wires DI, the `Channel.Type` enum string, and a test stub. |
| `add-a-data-source` | `.github/skills/add-a-data-source/` | SKILL.md: implement `IDataSource`, add a row to the admin UI, add a migration, add a worker registration. |
| `add-a-matcher` | `.github/skills/add-a-matcher/` | SKILL.md: extend the alert-matching engine with a new filter type. Includes a unit-test scaffold and an integration-test scaffold. |
| `seed-admin-user` | `.github/skills/seed-admin-user/` | SKILL.md + `seed-admin.csx` script: creates the first admin via env vars, idempotent. |
| `rbac-audit` | `.github/skills/rbac-audit/` | SKILL.md: a script that diffs the RBAC policy file vs. the controller `[Authorize]` attributes and reports gaps. |

Skills vs. instructions: instructions are passive (apply by file pattern). Skills are active (you invoke them when you want a workflow). Both belong in the harness.

### 1.5 Hooks (guardrails)

Hooks are scripts that run before/after certain Copilot events. We keep the set **minimal** — every hook is a guardrail we actually need.

| Hook event | When it runs | What it does |
|---|---|---|
| `pre-tool:run_in_terminal` | Before any terminal command | Reject commands that match: `rm -rf`, `git push --force origin main`, `dotnet ef database update` against a non-dev connection, anything with `--env production` in the args, anything that resolves a real API key from a known provider. |
| `post-tool:run_in_terminal` | After a `dotnet ef migrations add` | Verify the migration has both `Up` and `Down` methods, and that the model snapshot is committed. |
| `pre-tool:create_file` / `edit_file` | Before writing a new or modified file | Reject edits to: `appsettings.Production.json`, `.env` (only `.env.example` allowed), the RBAC seed data (`Permissions.cs` constants without a migration), `**/Migrations/*.cs` (only allowed via `dotnet ef` CLI; manual edits rejected). |
| `post-tool:create_file` / `edit_file` | After writing a file under `backend/**/Migrations/**` | Run `dotnet build` and `dotnet test --filter Category=Database` to catch broken migrations early. |
| `pre-commit` (Git hook, installed by `scripts/dev.sh`) | Before `git commit` | Run `dotnet format --verify-no-changes` and `pnpm lint`. Block the commit if they fail. |

The implementation: a small `.githooks/` directory checked in, with `.git config core.hooksPath .githooks`. The Copilot-side hook config is in `.github/hooks/hooks.json` (per the awesome-copilot Hooks schema).

### 1.6 MCP servers (free, local, minimal)

MCP servers we will run locally for the harness to do its job:

| Server | Purpose | Free? |
|---|---|---|
| **Filesystem** (built-in VS Code) | Already there. | Yes |
| **SQLite MCP** | Lets the implementer agents read the dev DB and run queries. | Yes |
| **MailHog MCP** *(optional)* | Lets the e2e agent check that an email was "sent" in dev. | Yes (we already run MailHog) |

We do **not** install:
- GitHub MCP (we have the local git CLI; not needed for a 24h MVP).
- A web-search MCP (the `Stack Doc Researcher` agent uses `fetch_webpage` directly).

### 1.7 VS Code tasks (one-click dev)

A `.vscode/tasks.json` so the user can `Ctrl+Shift+P → Run Task → …`:

| Task | What it does |
|---|---|
| `dev: up` | Runs the `dev.sh` / `dev.ps1` script. |
| `dev: reset-db` | Deletes `data/sonrisa.db`, re-runs migrations, re-seeds the admin user. |
| `dev: tail-logs` | Tails Api + Worker logs interleaved. |
| `dev: open-mailhog` | Opens the MailHog UI in the browser. |
| `dev: open-swagger` | Opens the backend Swagger UI. |
| `db: migration add` | Prompts for a name, runs `dotnet ef migrations add <name>`. |
| `db: migration apply` | Runs `dotnet ef database update`. |
| `test: all` | Runs `dotnet test` + `pnpm test` + `pytest`. |
| `test: e2e` | Runs Playwright. |

A launch configuration in `.vscode/launch.json` lets the user F5-debug the Api and the Worker separately. Both `launch.json` and `tasks.json` are committed (they're project config, not personal).

### 1.8 CI (GitHub Actions, free for public repos)

`.github/workflows/ci.yml`:

```
on: [push, pull_request]
jobs:
  lint:        … runs ESLint, dotnet format --verify, ruff
  backend:     … runs dotnet test on Linux + Windows (matrix)
  frontend:    … runs pnpm test, pnpm build
  sidecar:     … runs pytest
  e2e:         … runs Playwright against a background MailHog process
```

We deliberately do **not** include a "deploy" job in MVP. Deploy is a manual `dotnet publish` + copy to the user's host, run as a `systemd` unit / Windows Service.

---

## 2. The workflow (how you use this)

### 2.1 Starting a new feature

1. You write a one-paragraph brief in the issue ("As a user, I want X").
2. Open Copilot Chat, switch to the **TDD C#** or **TDD Next.js** implementer agent.
3. Tell it: "Implement issue #N. Follow the test-first pattern. Use the `add-a-channel` skill if relevant."
4. The agent:
   - Reads `AGENTS.md` + relevant `.instructions.md`.
   - Reads the matching `skills/` folder.
   - Writes a failing test.
   - Writes the minimal code to pass.
   - Refactors.
   - Updates the OpenAPI doc (backend) or the OpenAPI client types (frontend, if applicable).
   - Hands off to the **reviewer** agent.
5. The reviewer agent produces a structured review. You glance at it, accept or push back.
6. You commit. CI runs.

### 2.2 Adding a new channel (the canonical skill demo)

```
You:    "Add a Discord channel."
Agent:  (loads `add-a-channel` skill, follows checklist)
        - Creates services/discord/DiscordChannel.cs implementing INotificationChannel
        - Registers it in DI
        - Updates ChannelType constants
        - Writes unit tests (contract + happy path)
        - Asks the user to provide the webhook URL secret strategy
You:    Confirms or edits
Agent:  Hands off to backend-reviewer
```

### 2.3 When the agent gets it wrong

You have three escalation paths, in increasing order of friction:
1. **Just say "no"** in the chat. The agent re-attempts.
2. **Switch to the Reviewer** agent and ask "what's wrong with this diff?"
3. **Edit the relevant `instructions.md`** so the same mistake doesn't happen again. This is the *learning loop* — the harness gets sharper the more you use it.

### 2.4 Updating docs

Per your memory rule: **the agent never auto-updates `1-features.md`**. It can open a PR titled `docs(roadmap): mark X as done` and you merge or close it.

---

## 3. The safety nets (what we don't let the agent do)

| Forbidden action | How it's blocked |
|---|---|
| Push to `main` directly | `pre-tool` hook rejects `git push --force` and direct pushes; we work on branches. |
| Edit `appsettings.Production.json` | `pre-tool` hook rejects writes to that file. |
| Edit `.env` (only `.env.example` allowed) | `pre-tool` hook. |
| Run `dotnet ef database update` against a non-dev connection | `pre-tool` hook inspects the connection string. |
| Run `rm -rf` outside `node_modules`/`bin`/`obj` | `pre-tool` hook. |
| Skip a failing test | `pre-commit` hook runs `dotnet test --no-build`; commit blocked if any test fails. |
| Hand-edit a migration after it's been merged | `pre-tool` hook rejects edits to `backend/**/Migrations/*.cs`; migrations are owned by the CLI. |
| Add a real secret to the repo | `secrets.instructions.md` + `pre-tool` regex check on common secret patterns. |
| Modify the RBAC catalog silently (new role / permission without a migration) | `pre-tool` hook rejects; only allowed via a labeled `feat(rbac): …` migration. |
| Use a paid data provider | `AGENTS.md` non-negotiable; reviewer agent checks. |

All of these are belt-and-suspenders. The point isn't paranoia; it's that in a 24-hour build, you don't have time to recover from a botched `rm` or a leaked key.

---

## 4. The 24-hour timeline (how the harness pays for itself)

A reasonable 24h with this harness:

| Hour | Activity | Harness helps by… |
|---|---|---|
| 0–1 | Lock features, stack, AI env (this doc) | — |
| 1–2 | Scaffold monorepo, run `dev.sh`, see "Hello world" end-to-end | Tasks and dev script |
| 2–4 | Auth (signup, signin, refresh) with tests | TDD implementer + xUnit skills |
| 4–6 | Channel abstraction + EmailChannel + SlackChannel | `add-a-channel` skill |
| 6–8 | Alerts CRUD + filters UI | TDD implementer + RBAC instructions |
| 8–10 | News poller + matcher | Domain instructions |
| 10–12 | Market poller via yfinance sidecar | Python instructions + sidecar skill |
| 12–14 | Disaster poller | Domain instructions |
| 14–16 | Notification dispatcher + digests | Channel instructions |
| 16–18 | Admin: sources, users, health, announcements | RBAC + reviewer agent |
| 18–20 | Onboarding wizard (3-path) | Frontend reviewer |
| 20–22 | E2E tests, polish, edge cases | Playwright skills |
| 22–24 | Docs, README, deploy script | `roadmap-sync` agent (user-approved) |

The harness **shaves time** off every step from hour 2 onward. It does not add time.

---

## 5. What we **don't** put in the harness (and why)

- **A custom LLM or vector store.** We use Copilot as-is. No RAG, no fine-tuning. Not in MVP scope.
- **Auto-PR creation on every commit.** Too noisy. The agent opens a PR when the user asks.
- **A monorepo task runner (Turborepo, Nx).** Overkill for three projects. `dev.sh` + VS Code tasks is enough.
- **Pre-commit secret scanning with a paid service.** GitHub's free `secret-scanning` for public repos is enough.
- **A code-formatter war.** Use the defaults: `dotnet format`, `prettier`, `ruff`. One config file each. No debate.
- **A wiki.** The docs in `docs/roadmap/` are the wiki. The harness points to them.

---

## 6. Resolved decisions (locked in)

1. **Copilot instruction granularity**: **8 `.instructions.md` files**, one per concern. Auto-apply by file pattern keeps the context clean and the rules targeted.
2. **Custom agents**: **all 7** — TDD C# Implementer, TDD Next.js Implementer, Backend Reviewer, Frontend Reviewer, Stack Doc Researcher, Triage, Roadmap Sync. Each maps to a real workflow; no dead agents.
3. **Pre-commit hook strictness**: **block on lint/test failure.** Warnings get ignored in a 24h rush; blocks force the fix.
4. **MCP servers**: **SQLite MCP + MailHog MCP**, both free and local. No GitHub MCP, no web-search MCP in MVP.
5. **MUI lint rules**: **`eslint-plugin-mui` enabled** to catch the easy mistakes (missing `sx`, prop typos, deprecated APIs).
6. **CI matrix**: **Linux + Windows** for the backend. Catches path-separator and OS-specific bugs early; cost is acceptable for a free public repo.

All three roadmap documents are now locked. Next step: the bootstrap script that creates every file in this harness with its initial content, then we can start building.

---

## 7. Next step

If you give the go-ahead, the bootstrap script will create:
- `AGENTS.md`, `.github/copilot-instructions.md`
- 8 `.github/instructions/*.instructions.md` (one per concern)
- 7 `.github/agents/*.agent.md` (one per persona)
- 5 `.github/skills/*/SKILL.md` (project-specific)
- `.githooks/pre-commit` and `.github/hooks/hooks.json`
- `.vscode/tasks.json` and `.vscode/launch.json`
- `.github/workflows/ci.yml`
- A minimal `AGENTS.md` index for the awesome-copilot plugins we install

Estimated bootstrap time: ~20 minutes of file creation. The harness is then live and the 24h timer can start.
