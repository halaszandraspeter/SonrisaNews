# AGENTS.md — Sonrisa News

> **Loaded by**: all AI agents working in this repo (Copilot CLI, VS Code chat, Copilot coding agent, subagents).
> **Style**: short, declarative, links to canonical docs rather than restating them. If a rule conflicts with a more specific `.instructions.md` for the current file pattern, the more specific rule wins.

## 1. Mission

Build **Sonrisa News** — a free, open-source alert service that lets users get notified (email + Slack) when important things happen in the world: breaking news, market movements, natural disasters.

Canonical product description: [docs/roadmap/1-features.md](docs/roadmap/1-features.md).
Canonical stack: [docs/roadmap/2-stack.md](docs/roadmap/2-stack.md).

**Reliability is the number-one priority.** It is a service people trust to tell them when something important happened. The system must not silently lose notifications, leak data, or surprise the user.

## 2. Non-negotiables

- **No cloud subscription, no paid data providers.** Free tiers only. yfinance for quotes, USGS/GDACS/NHC for disasters, RSS for news, Resend/MailHog for email, Slack incoming webhooks. Document any free-tier limitation in the UI ("data delayed up to 15 minutes").
- **No Docker, no containers** in dev or in MVP packaging. Everything runs as a native process. MailHog is the Go binary, Postgres is installed natively and packaged as `systemd` unit / Windows Service. The .NET Aspire AppHost orchestrates the dev process model and can later publish Docker Compose / Kubernetes manifests via `AddDockerComposePublisher` / `AddKubernetesPublisher` if the deployment target changes.
- **No UI library other than MUI v9.** No Tailwind, no shadcn, no Chakra. Stick to the chosen stack.
- **No hand-edits to EF Core migrations.** Migrations are owned by `dotnet ef migrations add`. A `pre-tool` hook blocks manual edits.
- **No real secrets in the repo.** `.env` is gitignored; `.env.example` is the only checked-in env file. Use `dotnet user-secrets` locally. Production secrets come from env vars.
- **No `async void` in C#.** No `await` without `CancellationToken` in long-running paths. No blocking I/O in async methods.
- **Every controller endpoint declares `[ProducesResponseType]` for success and the relevant 4xx.** The OpenAPI doc is the contract with the frontend; codegen keeps them in sync.
- **Tests must pass in CI before merge.** No `it.skip`, no `dotnet test --filter Category=DoNotRun`. If a test is broken, fix it.
- **The user has the final word.** When a rule conflicts, ask. The user is named in the chat and answers.

## 3. Repo map

| Path | Holds |
|---|---|
| `docs/roadmap/` | The three locked planning documents: features, stack, AI environment. |
| `web/` | Next.js 16 (App Router) + React 19 + TypeScript + MUI v9. Frontend only. |
| `backend/` | .NET 10 solution. `Api` + `Worker` + `Domain` + `Infrastructure` + `AppHost`. |
| `services/yfinance/` | Python sidecar (FastAPI + yfinance). The only non-TS, non-C# code in the repo. |
| `deploy/` | Future: `systemd/` units, `windows-service/` installer. Empty in MVP. |
| `scripts/` | `dev.sh` / `dev.ps1` one-command start. |
| `.github/` | Workflows, instructions, agents, skills, hooks. **The AI harness lives here.** |
| `.vscode/` | `tasks.json` and `launch.json`. Committed. |

## 4. Folder conventions (match the user's "use the app → find the code" rule)

- **Backend (`backend/src/`)**: `Domain/` (entities, value objects, enums — no I/O), `Infrastructure/` (DbContext, repositories, external clients, channel implementations), `Api/` (controllers, middleware, auth, OpenAPI), `Worker/` (background services, pollers, matcher, dispatcher), `AppHost/` (Aspire orchestrator), `Shared/` (errors, options, cross-cutting helpers).
- **Frontend (`web/`)**: `app/` (Next.js routes, grouped by audience: `(marketing)`, `(app)`, `(admin)`, `(auth)`), `components/` (shared UI), `features/` (domain modules: `alerts`, `channels`, `sources`, `onboarding`…), `lib/` (api client, query client, auth helpers), `styles/`, `public/`.
- **Skills** live in `.github/skills/<skill-name>/SKILL.md` with optional `references/` and `assets/`.
- **Agents** live in `.github/agents/<name>.agent.md`.
- **Instructions** live in `.github/instructions/<scope>.instructions.md` with an `applyTo` frontmatter.

## 5. PR rules

- One PR per concern. Small enough to review in 5 minutes.
- **Each step of implementation = a clearly worded commit.** A "step" is one logical unit of work (a feature, a wave sub-task, a doc edit, a config change). The commit message is the human-readable record: subject line in the imperative mood, body explains *what* and *why*, footer references the issue / PR / checklist item.
- **Commits go through the GitHub MCP, not local git.** The agent creates a feature branch and uses the GitHub MCP server's `create_or_update_file` / `push_files` / `create_branch` tools to write files and produce one commit per logical step directly on the remote branch. Local `git commit` is reserved for the user.
- **The user opens the PR explicitly.** The agent does **not** push, open, or merge a PR on its own. When the user says "this is PR ready", the agent uses the GitHub MCP `create_pull_request` tool to open the PR with a clear title and body, then prints the PR URL for the user to review. The user merges.
- PR body must include:
  - **What changed** (1–3 bullets)
  - **How verified** (commands run, screenshots, test names)
  - **Rollback plan** (revert this commit, or a follow-up is required)
  - **Linked step(s)** from [`docs/implementation/mvp-checklist.md`](docs/implementation/mvp-checklist.md) — e.g. `Implements: Wave 3, step "Sign-up controller"`
- CI must be green. The harness blocks the merge otherwise.
- No force-pushes to `main`. Branch protection is on.

## 6. When in doubt

- **Ask the user.** Open a chat message, name the question, propose two options with a recommendation. The user picks.
- **Do not auto-update `docs/roadmap/1-features.md` or `2-stack.md`.** Open a PR with a "docs(roadmap): …" title; the user merges.
- **Do not invent an API.** If the canonical docs aren't clear, run the `Stack Doc Researcher` agent or say "I don't know — let me check."

## 7. Cross-references

- [docs/roadmap/1-features.md](docs/roadmap/1-features.md) — what we're building
- [docs/roadmap/2-stack.md](docs/roadmap/2-stack.md) — how we're building it
- [docs/roadmap/3-ai-environment.md](docs/roadmap/3-ai-environment.md) — the harness that builds it
- [`.github/copilot-instructions.md`](.github/copilot-instructions.md) — same rules, Copilot-specific hooks
- [`.github/instructions/`](.github/instructions/) — per-file-pattern rules
- [`.github/agents/`](.github/agents/) — named agent personas
- [`.github/skills/`](.github/skills/) — on-demand workflow skills
