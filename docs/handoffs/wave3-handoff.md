# Wave 3 → Wave 4+ Handoff — Auth + RBAC

> **From**: Wave 3 implementer (auth + RBAC, DB-driven).
> **To**: Wave 4+ implementers (alerts, channels, sources, users admin, matcher worker, dispatcher, audit, health).
> **Status**: Wave 3 is done. The auth + RBAC substrate is in `20260605051544_AddRbacCatalog` migration (applied to `data/sonrisa.db`). The items below are deferred because they require a future wave's controller or service to make the right call, OR are minor lint the implementer chose not to spend the wave on.

## What landed in wave 3

### Domain
- `backend/src/SonrisaNews.Domain/Auth/`
  - `Role.cs`, `Permission.cs`, `UserRole.cs`, `RolePermission.cs` — 4 new entities. `UserRole` and `RolePermission` are joins with composite PKs.
  - `RolesCatalogSeed.cs` — deterministic Guids for the 3 baseline roles (`User` = `11111111-…`, `Admin` = `22222222-…`, `System` = `33333333-…`) and the 16 MVP permissions. **The migration and the auth flow reference the same Guid literals — do not change them without a migration that deletes + re-inserts the seeded rows.**
  - `Roles.cs` — string constants `Roles.User`, `Roles.Admin`, `Roles.System`. Used for branching on role *names* in tests; the application code never branches on these.
  - `Permissions.cs` — 16 string constants (Alerts.Read.Own, Alerts.Write.Own, Alerts.Read.Any, Alerts.Write.Any, Channels.Read.Own, Channels.Write.Own, Sources.Read.Any, Sources.Write.Any, Users.Read.Any, Users.Write.Any, Users.Suspend, AuditLog.Read, Announcements.Write, Health.Read, Matcher.Run, Profile.Read).
- `backend/src/SonrisaNews.Domain/Entities/User.cs` — dropped the `Role` enum column. The `User` row no longer carries a role; role membership is a row in `UserRoles`.
- `backend/src/SonrisaNews.Domain/Enums.cs` — removed the `UserRole` enum (User/Admin/System). The remaining enums (AlertType, DeliveryMode, ChannelType, SourceType, UserStatus, NotificationStatus) are unchanged.

### Infrastructure
- `backend/src/SonrisaNews.Infrastructure/Persistence/Configurations/`
  - `RoleConfiguration.cs`, `PermissionConfiguration.cs`, `UserRoleConfiguration.cs`, `RolePermissionConfiguration.cs` — EF mappings. `UserRole` has FK→User Cascade + FK→Role Restrict; `RolePermission` has FK→Role Cascade + FK→Permission Restrict. The user→role FK is Cascade so deleting a user cleans up their grants; the role→user FK is Restrict so deleting a role errors out (you must revoke the role from every user first).
- `backend/src/SonrisaNews.Infrastructure/Persistence/SonrisaNewsDbContext.cs` — added 4 DbSets (`Roles`, `Permissions`, `UserRoles`, `RolePermissions`).
- `backend/src/SonrisaNews.Infrastructure/Auth/`
  - **`RbacPolicyHandler.cs`** — singleton `AuthorizationHandler<PermissionRequirement>`. Joins `UserRoles ⨝ RolePermissions ⨝ Permissions` filtered by the current user id and the permission name, with a per-instance `ConcurrentDictionary<CacheKey, bool>` cache. Cache is process-wide; invalidates at restart (deliberate — see "Deferred" §1).
  - **`AuthServiceCollectionExtensions.cs`** — registers the handler as singleton, plus the JWT bearer middleware, password hasher (BCrypt, work factor 12), token hasher (SHA-256), JWT issuer, refresh-token service, audit log, current-user, and `AdminSeeder` as `IHostedService`. The `Authorization` options add a fallback policy `RequireAuthenticatedUser` (so every controller endpoint requires an authenticated user by default; explicit `[AllowAnonymous]` opts out).
  - **`PermissionRequirement.cs`** — `IAuthorizationRequirement` carrying the permission name. The `AddPolicy(name, policy => policy.Requirements.Add(new PermissionRequirement(name)))` call lives in `AddSonrisaNewsPolicies` (same file).
  - **`AdminSeeder.cs`** — `IHostedService` that runs after migrations. Reads `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` from `IConfiguration`; fails fast on startup if either is missing. Idempotent in three cases: (a) no user → insert user + grant `UserRoles(Admin)`; (b) user without grant → insert grant; (c) user with grant → log info, no-op. The seeded user has `MustChangePassword = true`.
  - `ICurrentUser`, `HttpContextCurrentUser`, `IAuthTokenService`, `JwtAuthTokenService`, `IAuthService`, `AuthService`, `IRefreshTokenService`, `RefreshTokenService`, `IAuditLog`, `AuditLogService`, `IEmailSender`, `LoggingEmailSender`, `IPasswordHasher`, `BCryptPasswordHasher`, `ITokenHasher`, `Sha256TokenHasher`, `JwtOptions`, `IEmailVerificationService`, `EmailVerificationService` — all the auth plumbing from before, unchanged in shape.
- `backend/src/SonrisaNews.Infrastructure/SonrisaNews.Infrastructure.csproj` — Casbin package removed.
- `backend/src/SonrisaNews.Infrastructure/Migrations/20260605051544_AddRbacCatalog.cs` — forward-only migration. Drops `User.Role`, adds `User.MustChangePassword`, creates 4 new tables with FKs and unique indexes, seeds 3 roles + 16 permissions + 26 role↔permission grants (5 User + 16 Admin + 5 System). `// Forward-only after merge.` marker present. `Down()` reverts the schema; the 26 seeded rows are purged by `DropTable` (not by `DeleteData`).
- **Deleted**:
  - `backend/src/SonrisaNews.Infrastructure/Auth/casbin_model.conf`
  - `backend/src/SonrisaNews.Infrastructure/Auth/rbac_policy.csv`
  - `backend/src/SonrisaNews.Infrastructure/Auth/IEnforcerBootstrap.cs`
  - `backend/src/SonrisaNews.Infrastructure/Auth/FileEnforcerBootstrap.cs`
  - `backend/src/SonrisaNews.Infrastructure/Auth/RoleGroupingService.cs`
  - `backend/src/SonrisaNews.Infrastructure/Auth/IRoleGroupingService.cs`
  - `backend/src/SonrisaNews.Api/Auth/RbacHydrationHostedService.cs` (DB is the source of truth; no in-memory mirror to hydrate)

### API
- `backend/src/SonrisaNews.Api/Program.cs` — removed the `RbacHydrationHostedService` registration. `AddSonrisaNewsInfrastructure()` + `AddSonrisaNewsAuth(builder.Configuration)` + `AddSonrisaNewsPolicies()` are the three lines that wire it.
- `backend/src/SonrisaNews.Api/Controllers/AuthController.cs` — `POST /api/v1/auth/{signup,verify,signin,refresh,signout,forgot,reset}`. `SignInResponse` and `SignUpResponse` no longer carry a `Role` field (the user rule 2026-06-05 says validation is by permission, never by role; the response shape reflects that). The refresh-token rotation is unchanged.
- `backend/src/SonrisaNews.Api/Controllers/MeController.cs` — `GET /api/v1/me` returns `{ UserId, Email }` (no `Role`). `GET /api/v1/me/permissions` returns the caller's permission set as `IReadOnlyList<string>` — the frontend's source of truth for "may I show this button?".
- `backend/src/SonrisaNews.Api/Controllers/HealthController.cs` — `GET /healthz` liveness + `GET /readyz` readiness (DB ping). [AllowAnonymous] for both.

### Tests
- `backend/tests/SonrisaNews.UnitTests/Auth/RbacPolicyHandlerTests.cs` — 7 tests, all green. Positives: `Admin_CanSuspendUsers`, `User_CanReadOwnAlerts`, `System_CanRunMatcher`. Negatives: `RegularUser_CannotSuspendUsers`, `User_CannotManageSources`, `Unauthenticated_Request_IsRejected`. Plus `SecondRequest_ForSameUser_UsesTheCache_ButDoesNotLeakAcrossUsers` — proves the cache key includes the user id, so Alice's grant never leaks to Bob.
- `backend/tests/SonrisaNews.UnitTests/Auth/AdminSeederTests.cs` — 5 tests, all green. `NoExistingUser_CreatesAnAdmin` asserts a `UserRoles(Admin)` row is inserted alongside the `Users` row. `ExistingUserWithoutAdminRole_AddsAdminRole` and `ExistingUserWithAdminRole_DoesNotDuplicateGrant` cover the idempotency variants. `MissingEmail_Throws` and `MissingPassword_Throws` cover the fail-fast contract.
- `backend/tests/SonrisaNews.UnitTests/DatabaseSchemaTests.cs` — updated to remove `Role = UserRole.User` from the User round-trip; added a parallel `RbacCatalog_RoundTripsThroughSqliteMemory_AsExpected` test.

### Tools
- `backend/tools/RbacAudit/` — rewritten. Reads the `Permissions` table from the SQLite DB, walks every `[Authorize(Policy = "..." | Permissions.X)]` attribute in the controllers, resolves `Permissions.X` to its string value via the `Domain/Auth/Permissions.cs` constants, and diffs. **Fail = a `[Authorize]` references a permission that is not in the DB.** **Warn = a permission in the DB has no controller using it yet** (expected during the build-out — wave 4 (alerts) eats 4, wave 5 (channels) eats 2, wave 7 (sources + users + audit + announcements + health) eats the rest).
- `.vscode/tasks.json` — `rbac: audit` task now invokes `dotnet run --project backend/tools/RbacAudit -- --db data/sonrisa.db --controllers backend/src/SonrisaNews.Api`. The old `--policies rbac_policy.csv` flag is gone.

### Total test count
- 45 test cases passing (33 `[Fact]` + `[Theory]` methods, the extra 12 are xUnit data-row expansions from the two `[Theory]` in `DatabaseSchemaTests` and the one in `SonrisaNewsDbContextRegistrationTests`), 0 failing, 0 skipped. Wave 3 added 13 test methods: 7 in `Auth/RbacPolicyHandlerTests.cs`, 5 in `Auth/AdminSeederTests.cs`, 1 in `DatabaseSchemaTests.cs` (the `RbacCatalog_RoundTripsThroughSqliteMemory_AsExpected` test).
- `dotnet build SonrisaNews.slnx` → 0 warnings, 0 errors.
- `dotnet ef database update` against `data/sonrisa.db` → migration applied; the `PRAGMA foreign_keys = 0` warning is the documented SQLite x-foreign-keys quirk, not a failure.
- `dotnet run --project backend/tools/RbacAudit -- --db data/sonrisa.db --controllers backend/src/SonrisaNews.Api` → PASSED with 15 orphan permissions (all future-wave).

## User rule (2026-06-05) — restated for the next wave

**Validation to a resource is always via the permission, never via the role.** The codebase enforces this in three layers:
- `ICurrentUser` no longer exposes a `Role` property. The JWT does not carry a `Role` claim.
- The only authorization attribute allowed on a controller is `[Authorize(Policy = Permissions.X)]`. `[Authorize(Roles = "Admin")]`, `RequireRole(...)`, and `if (user.Role == ...) ??` are forbidden.
- `RbacPolicyHandler` answers only the coarse question "may a user with these roles perform this action on the resource class at all?" — resource ownership (e.g. "this alert belongs to the caller") is enforced at the service layer, not here.

If wave 4+ needs a new permission, the steps are:
1. Add a constant to `backend/src/SonrisaNews.Domain/Auth/Permissions.cs` (e.g. `Alerts.Read.Any`).
2. Decide which role(s) get it; update the `Role → Permission grants` block in the next migration's `Up()`.
3. Add `[Authorize(Policy = Permissions.AlertsReadAny)]` to the controller action.
4. Add a positive + negative test in `RbacPolicyHandlerTests` (per the tripwire in `.github/instructions/rbac-policies.instructions.md`).
5. Run `dotnet run --project backend/tools/RbacAudit --` and confirm the audit passes.

If wave 4+ needs a new *role* (e.g. `Moderator`):
1. Add the name to `backend/src/SonrisaNews.Domain/Auth/Roles.cs` (string constant).
2. Pick a deterministic Guid for the new role row in `RolesCatalogSeed` (the convention is `0..…​0` for the first 3; pick `44444444-…` for the next, or use a fresh `Guid.NewGuid()` literal and add a one-line comment explaining the choice).
3. Add a row in the next migration's `Roles` insert; add the role→permission grants in the same migration.
4. The seeder does not need to change — the bootstrap admin is always the `Admin` role.

## The migration marker

Per `.github/instructions/database-migrations.instructions.md` and `AGENTS.md §2`, the `// Forward-only after merge.` comment in `Up()` is the tripwire. After this PR is merged, the `Up()` is never edited. The next wave's RBAC changes go in a new migration; the `Down()` may be amended (e.g. to drop a future column) but the `Up()` is locked.

## Deferred items (nits + future waves)

These are conscious deferrals. The implementer chose the simplest correct value to get wave 3 green and let the future wave refine it. **None block merge.**

### 1. RbacPolicyHandler cache invalidation

The cache is per-process and never invalidates. A role grant added at runtime is invisible until the API restarts. This is acceptable for MVP because role grants only change on rare admin actions, and the deploy cycle is the natural cache-bust point. If a future wave needs live invalidation:
- **Best**: a `IHostedService` that polls `UserRoles` + `RolePermissions` for a hash every N seconds, and rehydrates the cache when the hash changes. The poll is one query and amortized across requests.
- **Acceptable**: a signal flag (e.g. a `Channel<T>` or a `MemoryCache` with a TTL) that the admin endpoint bumps when it writes a new grant.
- **Avoid**: per-request DB hits. The cache exists to make auth O(1) on the hot path.

When the invalidation strategy is chosen, also add an integration test that asserts: after the admin writes a new grant, the next request from the affected user is allowed (without a restart). The unit tests do not cover this; the cache-leak test only proves the *current* shape of the cache key, not its lifetime.

### 2. RbacPolicyHandler log level on denial

Downgraded to `LogInformation` (was `LogWarning`) because a 403 on a permission a user doesn't have is the common path of the API, not an anomaly. The audit log row, written by the controller, is the security-grade record. If a future wave wants brute-force detection, the right place is the access-log stream (Serilog request logging already emits one line per request with `UserId` + `StatusCode`), not the auth handler.

### 3. RbacAudit tool: orphan permissions are warnings, not errors

The audit currently exits 0 when the only finding is "DB has 15 permissions no controller uses yet" (true at end of wave 3; expected to drop to 0 by end of wave 7). If a future wave wants to enforce "every permission must be used by a controller" as an error, flip the `orphanInDb` branch to `findings += orphanInDb.Length` and update the message. Until then, the warning line is a build-out indicator.

### 4. MeController injects SonrisaNewsDbContext directly

`MeController` injects `SonrisaNewsDbContext` for the `GET /api/v1/me/permissions` query. The csharp-dotnet.instructions.md convention is "service layer for writes"; reads are fine. If a future wave needs a write (e.g. `PATCH /api/v1/me/display-name`), introduce a `MeService` and route through that.

### 5. Permission constants matrix in AddSonrisaNewsPolicies is hand-maintained

`AuthServiceCollectionExtensions.AddSonrisaNewsPolicies` enumerates the 16 permission constants by hand. The list is repeated in two places (the policy registration and the migration's `InsertData(Permissions, …)`). If a future wave adds a 17th permission, both sites must change. A future hardening: a `IEnumerable<string> All()` extension on `Permissions` that returns the array, and a corresponding one in `RolesCatalogSeed` that returns the Guids, both keyed by a shared index. Skip until the count exceeds ~25.

### 6. The OpenAPI doc for `GET /api/v1/me/permissions` is implicit

The `PermissionsResponse` record is documented with `IReadOnlyList<string>`, but the OpenAPI emitter will render that as `{ "permissions": ["string", "string", ...] }` without per-element documentation. The frontend can read the names as opaque strings and compare against the constants it imports from the OpenAPI schema. No change needed today; if a future wave wants the schema to enumerate the legal values, add a `summary` on the property or generate a Zod enum from the same `Permissions` list.

### 7. JWT issuance does not include the user's permission set

The JWT carries `Sub` (user id) and `Email` only. The client asks `GET /api/v1/me/permissions` after sign-in. If a future wave wants to embed the permission set in the JWT to avoid the round-trip, add a `permissions` claim in `JwtAuthTokenService.IssueAccessTokenAsync` and extend the read-side join in `RbacPolicyHandler` to honor the claim if present. Skip for MVP; the round-trip is cheap and keeps the token small.

### 8. The seeder's hard-fail on missing SEED_ADMIN_EMAIL/PASSWORD

`AdminSeeder.StartAsync` throws on startup if either env var is missing. This is correct for the first deploy (operator must know about an unset seed), but it's awkward for local dev: every `dotnet run` of the API from a fresh clone throws. The wave-2 dev script (`scripts/dev.ps1 -Reset`) sets sane defaults, so in practice it works. If a future wave wants a "dev-only" escape hatch (e.g. skip the seeder if `ASPIRE_ENVIRONMENT=Development` and the env vars are missing), add a flag to `AdminSeederOptions` and gate the throw on it. Don't change the prod behavior.

### 9. `Default policy: RequireAuthenticatedUser` means `/healthz` is authenticated

`AddSonrisaNewsAuth` sets `options.FallbackPolicy = RequireAuthenticatedUser`, which makes *every* endpoint require auth unless explicitly opted out with `[AllowAnonymous]`. The health endpoints have `[AllowAnonymous]`, but a future developer who forgets the attribute on a new public endpoint will get a 401 in production. Add a startup-time `RouteEndpoint` check (or a smoke test in the integration suite) that asserts every endpoint *not* marked `[Authorize]` has an explicit `[AllowAnonymous]` — or, simpler, replace the fallback with a per-controller opt-in. Tradeoff; defer.

### 10. Postgres round-trip

The migration uses `TEXT` for Guid columns (SQLite's representation of `uniqueidentifier` / `uuid` is `TEXT`). On Postgres the equivalent type is `uuid`; the column will round-trip correctly because EF Core 10's SQLite provider emits `TEXT` and the Postgres provider emits `uuid` for the same `Guid` property. **Verified mentally, not via a Postgres integration test** (the 2-stack.md Postgres swap is post-MVP). When the Postgres migration lands, run the same migration on a Postgres 16 instance and confirm the seed data inserts without `invalid input syntax for type uuid`.

## Schema decisions that need a future wave to make right

| # | Decision | Why this wave | Why a future wave might change it |
|---|---|---|---|
| 1 | `UserRoles` FK→User is `Cascade`; FK→Role is `Restrict` | Lets the API hard-delete a user without orphan grants; prevents accidentally dropping a role still in use. | A future "soft-delete user" wave may want to convert the user FK to `Restrict` (so a user with grants can't be hard-deleted). |
| 2 | `RolePermissions` FK→Role is `Cascade`; FK→Permission is `Restrict` | Dropping a role purges its grants; you can't drop a permission that's still in use. | Wave 11 (polish) may add a `DeactivatedAt` column on `Permissions` instead of a hard drop, and change this to `Restrict` everywhere. |
| 3 | The seed `Permissions` insert sets a short human-readable `Description` for every row (e.g. `Alerts.Read.Own → "Read own alerts"`, `Users.Suspend → "Suspend a user (admin/audit)"`). | The catalog needs a Description column for the admin UI (wave 7) to render a tooltip; the seed fills it with first-pass copy. | Wave 7 (admin UI) may want to rewrite the copy in a follow-up migration if the operator-facing language needs more detail. |
| 4 | The seed `Roles` insert has `DisplayName = Name` for `User` and `System` | Both are short and self-explanatory; a `DisplayName` distinct from the `Name` would just be busy-work. | "Admin" → "Operator" rename is a wave-10 polish item; the migration supports it. |
| 5 | The role→permission grant matrix is **5 for User + 16 for Admin + 5 for System = 26 total rows** in `Migrations/20260605051544_AddRbacCatalog.cs:301-330`. User gets 5 self-service permissions (Alerts.Read.Own, Alerts.Write.Own, Channels.Read.Own, Channels.Write.Own, Profile.Read). Admin gets the 5 User permissions + 11 admin-only (Alerts.Read.Any, Alerts.Write.Any, Sources.Read.Any, Sources.Write.Any, Users.Read.Any, Users.Write.Any, Users.Suspend, AuditLog.Read, Announcements.Write, Health.Read, Profile.Read). System gets 5 (Matcher.Run, Alerts.Read.Any, Alerts.Write.Any, Sources.Read.Any, Profile.Read). | Matches the doc in `docs/roadmap/1-features.md` §6 verbatim. | A future "Moderator" role needs a subset of `Admin` minus `Users.Suspend` / `Users.Write.Any` / `Announcements.Write`; the matrix is additive. |
| 6 | `MeController` is the only wave-3 controller behind the `[Authorize(Policy = ...)]` attribute (uses `Profile.Read`) | Wave 3's only authenticated endpoint is "are you who you say you are?". | Wave 4+ will add many. The RbacAudit tool will track. |
| 7 | The migration timestamp `20260605051544_AddRbacCatalog.cs` is generated by `dotnet ef migrations add` | The CLI owns migrations. | The next wave's migration timestamp will be the next CLI run. |
| 8 | `RolesCatalogSeed` Guid literals are not `Guid.NewGuid()` | The application code and the migration must agree on row keys; random Guids would force a SELECT round-trip on the auth flow. | A future wave that needs to rename a role can keep the same Guid and update the `Name` column; the application code still works. |

## OpenAPI / frontend contract

- `MeResponse`: `{ userId: GUID, email: string }` — was `{ userId, email, role }`. Frontend `web/lib/api/schema.ts` will regenerate.
- `PermissionsResponse`: `{ permissions: string[] }` — new endpoint `GET /api/v1/me/permissions`. Frontend should call this on sign-in and cache.
- `SignInResponse` / `SignUpResponse`: dropped `role` field. Frontend was already reading `email` + `displayName`; the `role` field was display-only and is now gone.
- All other auth endpoints are unchanged.

The frontend must regenerate the OpenAPI client (`pnpm --dir web generate:api`) and rebuild. The dev script's `dev: up` task will fail loudly if the codegen is out of date.

**The wave-3 frontend handoff** lives at [`wave3-frontend-handoff.md`](./wave3-frontend-handoff.md). It covers the typed `web/lib/api/schema.ts` (hand-authored from this contract, regen-safe), the in-memory auth store + `AuthProvider` + middleware on `openapi-fetch`, the sign-in / sign-up / verify pages with Zod + React Hook Form, the `AuthGate` that replaced the redirect-in-layout, the boundary triplet, and the OpenAPI drift check script. Wave 4+ consumes that handoff.

## Push status (read me)

The wave-3 changes are in the working tree, **not committed, not on a branch, not pushed**. Per `AGENTS.md` §5, the agent does not push, open, or merge PRs on its own. The user opens the PR explicitly. The suggested commit shape (one logical step per commit, per `AGENTS.md` §5):

1. **`feat(rbac): DB-driven catalog (5 tables, no Casbin)`** — Domain entities + configurations + DbContext + `RolesCatalogSeed`. No controllers change.
2. **`feat(rbac): singleton DB-driven RbacPolicyHandler with cache`** — `RbacPolicyHandler.cs`, `ICurrentUser` (Role removed), JWT options (RoleClaimType removed), `AuthServiceCollectionExtensions` (Casbin registrations removed), `AdminSeeder` (UserRoles insert), `IAuthService.SignUpAsync` (UserRoles insert).
3. **`chore(rbac): drop Casbin package + delete casbin_model.conf / rbac_policy.csv`**.
4. **`feat(rbac): add `GetMyPermissions` endpoint + drop Role from auth responses`** — `MeController` /permissions, `AuthController` response DTOs, `Program.cs` (drop RbacHydrationHostedService).
5. **`feat(rbac): AddRbacCatalog migration with seed data`** — the `20260605051544_AddRbacCatalog.cs` migration (with `// Forward-only after merge.`), `SonrisaNews.Infrastructure.csproj` (no Casbin).
6. **`test(rbac): rewrite handler + seeder tests for DB-driven catalog`** — `RbacPolicyHandlerTests`, `AdminSeederTests`, `DatabaseSchemaTests` (RbacCatalog round-trip test).
7. **`feat(tool): DB-driven RbacAudit`** — `tools/RbacAudit/Program.cs` (real audit, not skeleton), `RbacAudit.csproj` (Microsoft.Data.Sqlite), `tasks.json` (new task arg).
8. **`docs(rbac): user-rule restated in Permissions.cs / PermissionRequirement.cs / RbacPolicyHandler.cs / AddRbacCatalog`** — the in-source doc comments that explain "validation is by permission, never by role".

The user applies and pushes. The PR body should reference `Implements: Wave 3, step "Auth + RBAC"` from `docs/implementation/mvp-checklist.md` so the checklist and the git log stay in sync.
