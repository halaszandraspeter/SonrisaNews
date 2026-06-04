---
description: 'Sonrisa News — Copilot Chat / coding agent ground rules. Mirrors AGENTS.md, adds Copilot-specific hooks.'
applyTo: '**'
---

# Sonrisa News — Copilot Instructions

> **Scope**: auto-applied to every file the Copilot agent touches in this repo.
> **Authoritative ground rules**: see [`AGENTS.md`](../AGENTS.md). This file is the Copilot-specific subset.
> **Per-language/per-layer rules**: see [`.github/instructions/`](./instructions/). The most specific rule wins.

## Mission (one-liner)

Build Sonrisa News — a free, open-source alert service. Reliability first. Source: [docs/roadmap/1-features.md](../docs/roadmap/1-features.md).

## Stack (one-liner)

- **Frontend**: Next.js 16 (App Router) + React 19 + TypeScript strict + **MUI v9** + React Query v5 + Zod.
- **Backend**: C# / .NET 10 LTS, ASP.NET Core controllers, EF Core 10, Casbin.NET for RBAC, MailKit for SMTP, Swashbuckle for OpenAPI.
- **Sidecar**: Python 3.12 + FastAPI + yfinance.
- **DB**: SQLite in MVP, Postgres-compatible schema.
- **Dev process model**: .NET Aspire AppHost, no Docker.

Source: [docs/roadmap/2-stack.md](../docs/roadmap/2-stack.md).

## Behavior hooks (apply to every Copilot turn)

1. **Before claiming "done" on a backend change**, run `dotnet test` from `backend/` and report the result. If any test fails, the change is not done.
2. **Before claiming "done" on a frontend change**, run `pnpm test` and `pnpm build` from `web/` and report the result.
3. **Before claiming "done" on the yfinance sidecar**, run `pytest` from `services/yfinance/`.
4. **When changing the DB schema**, run `dotnet ef migrations add <Name> --project backend/src/SonrisaNews.Infrastructure` and commit the migration file. Never hand-edit `backend/**/Migrations/*.cs` (a `pre-tool` hook will block it anyway).
5. **When adding or changing a controller endpoint**, declare `[ProducesResponseType]` for at least the success status and the 400/401/403/404 cases. Add an XML `<summary>`. The OpenAPI doc is the frontend's source of truth.
6. **When changing the OpenAPI doc**, regenerate the frontend client (`pnpm --dir web generate:api`) and check the diff. Drift is a bug.
7. **Never edit `appsettings.Production.json` directly.** Propose the change in the PR description and let the user apply.
8. **Never edit `.env`.** Only `.env.example` may be committed.
9. **Never edit `backend/src/SonrisaNews.Infrastructure/Auth/rbac_policy.csv` directly.** Open a labeled PR (`feat(rbac): …`); a `pre-tool` hook blocks silent edits.
10. **Never use paid data providers or cloud subscriptions.** If a task seems to require one, stop and ask the user.
11. **Never `git push --force` to `main`.** A `pre-tool` hook blocks it.
12. **Never `rm -rf` outside `node_modules`, `bin`, `obj`, `dist`, `.next`, `.pytest_cache`.** A `pre-tool` hook blocks anything else.

## The "RBAC + auth" tripwire

Any change to `backend/src/SonrisaNews.Infrastructure/Auth/`, `backend/src/SonrisaNews.Api/Auth/`, or `rbac_policy.csv` must include:

- A unit test demonstrating the new permission **works** for an authorized user.
- A unit test demonstrating the permission is **rejected** for an unauthorized user.

Both tests are mandatory. The reviewer agent will reject the PR if either is missing.

## The "channel" tripwire

Any new `INotificationChannel` implementation must:

- Implement the full interface (`Type`, `StartVerificationAsync`, `VerifyAsync`, `SendAsync`).
- Be registered in DI (`Program.cs` of the API).
- Have a unit test for the happy path AND a test for the verification-failed path.
- Update the `Channel.Type` constants so the frontend can render the correct icon.

Use the [`add-a-channel`](../.github/skills/add-a-channel/SKILL.md) skill.

## The "data source" tripwire

Any new `IDataSource` implementation must:

- Implement the full interface (`Id`, `Type`, `PollInterval`, `FetchAsync`).
- Be registered in DI in the Worker.
- Have a unit test that injects a fake `HttpMessageHandler` and asserts the parsed `RawEvent` shape.
- Add a row to the admin seed if it's a default-on source.

Use the [`add-a-data-source`](../.github/skills/add-a-data-source/SKILL.md) skill.

## The "schema" tripwire

The schema lives in `backend/src/SonrisaNews.Infrastructure/Persistence/`. Any entity change is a migration. Any migration:

- Has both `Up` and `Down` methods.
- Has a `// Forward-only after merge.` comment in `Up`.
- Is committed in the same PR as the entity change.
- Updates the model snapshot (`backend/.../Persistence/SonrisaNewsModelSnapshot.cs`) automatically — never hand-edit.

## Style quick-reference (full rules in `.github/instructions/`)

- **C#**: file-scoped namespaces, `var` for locals, expression-bodied members only when they fit on one line, `record` for DTOs, primary constructors on .NET 8+. Naming: `PascalCase` for types/methods/public members, `_camelCase` for private fields, `I` prefix for interfaces.
- **TS/React**: named exports only, no default exports for components. Functional components. `useQuery` / `useMutation` from `@tanstack/react-query`. Zod schemas colocated with the form. `sx` prop instead of `style={}` when the value is dynamic.
- **Python**: type hints everywhere. `async`/`await` only. `httpx.AsyncClient` injected, never constructed per call. Structured logging via `structlog`.

## When you get stuck

- **Don't guess APIs.** If you don't know the exact method signature or behavior, use the `Stack Doc Researcher` agent or read the canonical docs in the relevant `.instructions.md` file.
- **Don't silently change the stack.** If the user asked for MUI v9 and the dependency tree suggests v6, ask.
- **Don't auto-update roadmap docs.** Open a PR; the user merges.

## Commit and PR flow

a. **Each step of implementation = a clearly worded commit.** A "step" is one logical unit of work (a feature, a wave sub-task, a doc edit, a config change). The commit message is the human-readable record: subject line in the imperative mood, body explains *what* and *why*, footer references the issue / PR / checklist item.
b. **Commits go through the GitHub MCP, not local git.** The agent creates a feature branch and uses the GitHub MCP server to write files directly to it. **Default tool: `push_files`** (multi-file single commit, when the changes are logically one step). Use `create_or_update_file` only for one-off edits to a single file after the branch exists. Use `create_branch` for the initial branch. This keeps the commit log structured and avoids round-tripping through the local working tree.
c. **The user opens the PR explicitly.** The agent does **not** push, open, or merge a PR on its own. When the user says "this is PR ready", the agent uses the GitHub MCP `create_pull_request` tool to open the PR with a clear title and body, then prints the PR URL for the user to review. The user merges.
d. **The PR body must reference the wave / step** it implements. Format: `Implements: Wave N, step "..."` from [`docs/implementation/mvp-checklist.md`](../docs/implementation/mvp-checklist.md). This is how we keep the checklist and the git log in sync.
e. **The local pre-commit hook is a backup, not the primary guard.** It runs on local `git commit` — which we don't do for MCP-driven commits. After the user clones the repo, `scripts/dev.sh` wires up `.githooks/` via `git config core.hooksPath .githooks`. The hook guards future local commits and is defense-in-depth for the secret scan, file blocklist, and migration shape checks.

## Cross-references

- [AGENTS.md](../AGENTS.md) — authoritative ground rules
- [docs/roadmap/1-features.md](../docs/roadmap/1-features.md) — what we're building
- [docs/roadmap/2-stack.md](../docs/roadmap/2-stack.md) — how we're building it
- [docs/implementation/mvp-checklist.md](../docs/implementation/mvp-checklist.md) — the build order
- [.github/instructions/](instructions/) — per-file-pattern rules
- [.github/agents/](agents/) — named agent personas
- [.github/skills/](skills/) — on-demand workflow skills
