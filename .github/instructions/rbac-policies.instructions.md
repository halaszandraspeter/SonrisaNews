---
description: 'RBAC rules. The authorization policy file is the security boundary.'
applyTo: 'backend/**/Auth/**'
---

# RBAC — Sonrisa News

> **Read first**: [AGENTS.md](../../../AGENTS.md), [docs/roadmap/2-stack.md](../../../docs/roadmap/2-stack.md) §3 (RBAC).
> **Critical file**: `backend/src/SonrisaNews.Infrastructure/Auth/rbac_policy.csv` is the source of truth. Do not edit silently.

## Roles

Three roles, seeded from `appsettings.json` at startup:

- `User` — default. Can manage their own alerts, channels, profile.
- `Admin` — can manage data sources, users, announcements, system health. Audit-logged on every action.
- `System` — internal role for background workers. Cannot sign in.

## Permissions

Permissions are string constants in `backend/src/SonrisaNews.Domain/Auth/Permissions.cs`. Format: `<Resource>.<Action>.<Scope>`.

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

## Policy file format

`rbac_policy.csv` uses Casbin's CSV syntax:

```csv
# Resource, Action, Scope → which roles are allowed
p, Admin, *, *
p, User, Alerts, own
p, User, Channels, own
p, System, Matcher, run

# Role assignments
g, alice@example.com, Admin
g, bob@example.com, User
```

- One `p` line per role/resource/scope combination.
- `*` is a wildcard for the role section (Admin gets everything).
- `g` (grouping) lines assign users to roles. The user identity is the email, not the user id (we want stable identifiers).
- **The file is plain CSV, not YAML, not JSON.** Comments start with `#`.

## Tripwire (the agent must enforce this)

**Any change to `rbac_policy.csv` or any file in `backend/**/Auth/` must include:**

1. **A unit test that demonstrates the new permission works** for an authorized user.
2. **A unit test that demonstrates the new permission is rejected** for an unauthorized user.

Both tests are mandatory. The Backend Reviewer agent will reject the PR if either is missing.

Tests live in `backend/tests/SonrisaNews.UnitTests/Auth/` and use a fake `IHttpContextAccessor` plus a fresh `Enforcer` instance (don't share state between tests).

## The tripwire also applies when

- A new `[Authorize(Policy = "...")]` attribute is added to any controller. The new permission must be in the policy file.
- A new permission constant is added to `Permissions.cs`. It must be granted to at least one role in the policy file.

Use the [`rbac-audit`](../../skills/rbac-audit/SKILL.md) skill to verify there's no drift.

## The `RbacPolicyHandler`

The handler lives in `backend/src/SonrisaNews.Infrastructure/Auth/RbacPolicyHandler.cs`. It:

1. Reads the current user from the `HttpContext` (via `ICurrentUser`).
2. Reads the requested permission from the policy attribute.
3. Builds a Casbin request: `(sub=email, obj=resource, act=action, dom=scope)`.
4. Calls `enforcer.EnforceAsync(request)`.
5. On `false`, returns a 403 with a typed problem detail (RFC 7807).

The handler is registered in DI and is the **only** authorization enforcement point. Controllers do not re-check.

## Audit logging

Every admin action writes an `AuditLog` row via the `IAuditLog.RecordAsync` filter. The filter is registered globally on controllers under `(admin)/`. Non-admin actions are not audit-logged (reduces noise).

## Forbidden patterns (the agent must not introduce these)

- Editing `rbac_policy.csv` without a labeled PR (`feat(rbac): …`)
- Adding a new permission to `Permissions.cs` without granting it in the policy file
- `[Authorize(Roles = "Admin")]` (use `[Authorize(Policy = "...")]` with a constant)
- Re-checking ownership in the controller body (the handler does it)
- A role check that bypasses the policy handler (e.g. `if (user.IsAdmin)` in business logic)
- Storing the user's role in the JWT (the role is in the DB, the JWT carries the email, Casbin resolves)
- An `Admin` action that doesn't write to `AuditLog`
- Hard-coded role names in code (always use the `Roles` constants from `Auth/Roles.cs`)
