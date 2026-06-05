---
description: 'RBAC rules. The authorization policy is the security boundary — implemented in the DB, not in code or in a CSV.'
applyTo: 'backend/**/Auth/**'
---

# RBAC — Sonrisa News

> **Read first**: [AGENTS.md](../../../AGENTS.md), [docs/roadmap/2-stack.md](../../../docs/roadmap/2-stack.md) §3 (RBAC).
> **Source of truth**: the **database** — five tables (`Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`). The CSV policy file (Casbin) is **deprecated**; a follow-up PR removes it. This rule was set by the user on 2026-06-05 and supersedes the earlier "CSV is the source of truth" framing.

## The two non-negotiables

1. **Validation to a resource is always via the permission, never via the role.** — user, 2026-06-05.
2. **Every controller action that reads or writes data** declares `[Authorize(Policy = Permissions.X)]`. The handler does the work; controllers do not re-check.

## The five tables

The RBAC catalog and grants live in the DB. Every change ships in a labeled migration (`feat(rbac): …`); the migration is reviewed by the Backend Reviewer.

Table names follow the existing EF Core convention in the repo (`PascalCase`, pluralized entity name — see `AlertChannelModes`, `AuditLogs`, etc.):

| Table | Purpose |
|---|---|
| `Users` | The existing `User` entity. **No `Role` column.** The role is a row in `UserRoles`. |
| `Roles` | The role catalog. Seeded at migration time: `User`, `Admin`, `System`. |
| `Permissions` | The permission catalog. One row per constant in `Permissions.cs`. |
| `UserRoles` | Join table: `(user_id, role_id)`. The seed admin gets a row here on first startup. |
| `RolePermissions` | Join table: `(role_id, permission_id)`. The grants from the previous `rbac_policy.csv` (`Admin → *`, `User → Alerts/Channels`, `System → Matcher.Run`) become rows in this table. |

## Roles

Three roles, seeded by an `INSERT` in the initial migration:

- `User` — default. Can manage their own alerts, channels, profile.
- `Admin` — can manage data sources, users, announcements, system health. Audit-logged on every action.
- `System` — internal role for background workers. Cannot sign in.

## Permissions

String constants in `backend/src/SonrisaNews.Domain/Auth/Permissions.cs`. Format: `<Resource>.<Action>.<Scope>`. The catalog is the 16 constants in `Permissions.cs`:

| Permission | Who has it | Notes |
|---|---|---|
| `Alerts.Read.Own` | User, Admin | User can read their own alerts; Admin can read any. |
| `Alerts.Write.Own` | User, Admin | Same pattern. |
| `Alerts.Read.Any` | Admin only | |
| `Alerts.Write.Any` | Admin only | |
| `Channels.Read.Own` | User, Admin | |
| `Channels.Write.Own` | User, Admin | |
| `Sources.Read.Any` | Admin only | |
| `Sources.Write.Any` | Admin only | |
| `Users.Read.Any` | Admin only | |
| `Users.Write.Any` | Admin only | |
| `Users.Suspend` | Admin only | |
| `AuditLog.Read` | Admin only | |
| `Announcements.Write` | Admin only | |
| `Health.Read` | Admin only | |
| `Matcher.Run` | System only | Background worker only. |
| `Profile.Read` | User, Admin | Read-only view of the current user. |

## The handler

`backend/src/SonrisaNews.Infrastructure/Auth/RbacPolicyHandler.cs` is the only authorization enforcement point. The handler:

1. Reads the current user from the `HttpContext` (via `ICurrentUser`).
2. Reads the requested permission from the policy attribute.
3. Queries the DB: `SELECT 1 FROM UserRoles ur JOIN RolePermissions rp ON ur.RoleId = rp.RoleId WHERE ur.UserId = @userId AND rp.Permission = @permission LIMIT 1`.
4. On a hit, succeeds. On a miss, returns 403 with a typed problem detail (RFC 7807).

The result may be **cached per request** (one DB hit per user per request, not per `[Authorize]` attribute) — see the implementation for the cache shape. Invalidation is automatic because the cache lifetime is the request.

## Tripwire (the agent must enforce this)

**Any change to `backend/**/Auth/**` or to the role/permission entities or seed data must include:**

1. **A unit test that demonstrates the new permission works** for an authorized user.
2. **A unit test that demonstrates the new permission is rejected** for an unauthorized user.

Both tests are mandatory. The Backend Reviewer agent will reject the PR if either is missing.

Tests live in `backend/tests/SonrisaNews.UnitTests/Auth/` and use an in-memory SQLite `DbContext` plus a real `AuthorizationHandlerContext`. They are hermetic — no live network, no live Casbin, no live JWT issuer.

## The tripwire also applies when

- A new `[Authorize(Policy = "...")]` attribute is added to any controller. The new permission must exist in the `Permissions` table AND be granted to at least one role in `RolePermissions`.
- A new permission constant is added to `Permissions.cs`. It must have a row in `Permissions` (seeded by the migration) AND a grant in `RolePermissions`.
- A new role is added to `Roles.cs`. It must have a row in `Roles` and the relevant grants in `RolePermissions`.

Use the [`rbac-audit`](../../skills/rbac-audit/SKILL.md) skill to verify there's no drift.

## Audit logging

Every admin action writes an `AuditLog` row via the `IAuditLog.RecordAsync` filter. The filter is registered globally on controllers under `(admin)/`. Non-admin actions are not audit-logged (reduces noise).

## Forbidden patterns (the agent must not introduce these)

- Editing `RolePermissions` rows in code without a labeled migration
- Adding a new permission to `Permissions.cs` without a migration that seeds the `Permissions` row + the relevant `RolePermissions` row
- **`[Authorize(Roles = "Admin")]`** (use `[Authorize(Policy = "...")]` with a constant)
- **`[Authorize(Roles = "User")]`**, **`RequireRole("Admin")`**, or any role-based authorization
- **`if (user.Role == UserRole.Admin)`** or any role check in business logic
- **A role check that bypasses the policy handler** (the handler is the only enforcement point)
- **Storing the user's role in the JWT** (the role is in the DB; the JWT carries the user id, the handler resolves the roles + permissions from the DB per request)
- **A Casbin / CSV policy file** (deleted in the same PR that migrates to the 5-table model)
- An `Admin` action that doesn't write to `AuditLog`
- Hard-coded role names in code (always use the `Roles` constants from `Auth/Roles.cs`)
- **Validating "is this user an admin?" with a role check** (use a permission check against `Permissions.X.Any` instead)
