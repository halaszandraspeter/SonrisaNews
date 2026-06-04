---
description: 'C# / .NET coding standards for Sonrisa News. Reliability-first rules.'
applyTo: 'backend/**/*.cs'
---

# C# / .NET — Sonrisa News

> **Read first**: [AGENTS.md](../../../AGENTS.md), [docs/roadmap/2-stack.md](../../../docs/roadmap/2-stack.md) §3.

## Reliability rules (non-negotiable)

- **Never `async void`.** Use `async Task` or `async Task<T>`. `void` is allowed only on event handlers, and even then wrap in try/catch.
- **Always pass `CancellationToken`** through async chains. Background services, controllers, and hosted services all have one. Don't drop it.
- **No blocking I/O on async paths.** `Task.Wait`, `Task.Result`, `.GetAwaiter().GetResult()` are forbidden in service code (allowed in test setup only).
- **All public methods that touch I/O must be cancellable** AND must observe the token in a loop (so they exit promptly on shutdown).
- **No `Thread.Sleep`.** Use `await Task.Delay(ms, ct)`.
- **No fire-and-forget tasks** (`_ = SomeMethod()`). Either await it, or queue it in the `IHostedService` lifecycle.

## EF Core rules

- **Migrations are owned by the CLI.** `dotnet ef migrations add <Name> --project src/SonrisaNews.Infrastructure`. Never hand-edit `**/Migrations/*.cs`.
- **DbContext is scoped.** One per request in the Api, one per loop iteration in the Worker. Never a singleton.
- **No `IQueryable` returned from a service boundary.** Return `Task<List<T>>` or `IAsyncEnumerable<T>`. The query executes where it's composed.
- **Use `AsNoTracking()` for read-only queries.** Default tracking is wasteful in the worker.
- **Use `ExecuteUpdate` / `ExecuteDelete` for bulk operations** when you don't need change tracking.
- **All entity changes go through the `DbContext.SaveChangesAsync(ct)` path.** No raw ADO.NET outside the `Persistence/` layer.
- **No string-typed column comparisons in hot paths.** Use strongly-typed LINQ. If you must use raw SQL, use parameterized queries (no concatenation ever).

## RBAC rules

- **Every controller action that reads or writes data** must have an `[Authorize(Policy = "…")]` attribute. The policy name is a constant in `Permissions.cs`.
- **The policy name follows the pattern `<Resource>.<Action>.<Scope>`**: e.g. `Alerts.Write.Own`, `Sources.Write.Any`, `Users.Suspend`. The handler in `RbacPolicyHandler.cs` consults Casbin with `(subject, action, resource)`.
- **Resource ownership is enforced at the handler**, not in the controller. The handler reads `currentUser.Id` from the request and passes it as a Casbin domain. Don't repeat ownership checks in the controller body.
- **Admin actions write to `AuditLog`** via the `IAuditLog.RecordAsync(action, target, metadata, ct)` helper. It's called from a filter, not manually.

## Controller rules

- **Every action declares `[ProducesResponseType(typeof(TResponse), StatusCodes.Status200OK)]`** AND `[ProducesResponseType(StatusCodes.Status400BadRequest)]` minimum. Add `401/403/404/409` as relevant.
- **XML doc comments on every public action** with `<summary>`, `<param>`, `<returns>`. The OpenAPI doc is the frontend's source of truth.
- **No business logic in controllers.** Controllers parse, validate, call a service, return a result. The service does the work.
- **Use `FluentValidation` for input** (not data annotations). One validator per request DTO. Validator is registered in DI.
- **Never return `Ok(entity)` for entities that contain sensitive fields.** Use a response DTO.

## Channel / source implementations

- **`INotificationChannel` implementations** are tested with a fake SMTP / fake HTTP server, never against a real provider. See the `add-a-channel` skill.
- **`IDataSource` implementations** are tested with an injected `HttpMessageHandler` that returns canned responses. No live network in unit tests.
- **All external calls go through a typed `HttpClient`** registered via `AddHttpClient<TClient>(...)`. Don't `new HttpClient()`.

## Logging

- Use `ILogger<T>` injected, not `Console.WriteLine`.
- **Structured properties, not interpolated strings**: `_logger.LogInformation("Alert matched {AlertId} for event {EventId}", alertId, eventId)`, not `LogInformation($"Alert matched {alertId} for event {eventId}")`.
- **Never log a full `payload`** — log the `id` and a short summary. PII is real.

## Style

- File-scoped namespaces, `var` for locals, expression-bodied members only when they fit on one line.
- `record` for DTOs and value objects, `class` for entities with identity.
- Primary constructors on .NET 8+ for service classes with simple dependencies.
- Naming: `PascalCase` for types/methods/public members, `_camelCase` for private fields, `I` prefix for interfaces.
- **Named constants over magic numbers**, even single-use. The name is the documentation.
- **No commented-out code. No `#if false` blocks. No "for later" notes in code.** Git has history; the roadmap doc captures intent.

## Tests

- xUnit + FluentAssertions. One assertion concept per test (multiple `Assert` calls are fine if they verify one behavior).
- Test names: `MethodName_StateUnderTest_ExpectedBehavior`.
- **All external services are faked** (no live network, no live SMTP, no live Slack).
- **No real time in tests** — use `IClock` and a fake `ISystemClock`. The `TimeProvider` built into .NET 8+ is acceptable.

## Forbidden patterns (the agent must not introduce these)

- `async void` (except event handlers, and even then wrapped)
- `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` in service code
- `Thread.Sleep` in service code
- Direct `IQueryable` exposure from services
- Raw SQL with string concatenation
- `Console.WriteLine` in production code
- `new HttpClient()` (must use `IHttpClientFactory`)
- Singleton `DbContext`
- Logging with PII (email, password, full tokens)
- Hardcoded connection strings (must come from `IConfiguration`)
- Hand-edited migration files
- Silent `catch` blocks (always log; re-throw unless you have a deliberate reason)
