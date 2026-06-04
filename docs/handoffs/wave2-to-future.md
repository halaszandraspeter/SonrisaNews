# Wave 2 → Wave 3+ Handoff

> **From**: Wave 2 implementer (database + persistence skeleton).
> **To**: Wave 3+ implementers (auth, channels, alerts, matcher, cleanup, etc.).
> **Status**: Wave 2 implementer is done. The schema is in `InitialSchema` migration
> and applied to `data/sonrisa.db`. The items below are deferred because they
> require a wave that doesn't exist yet to make the right call.

## What landed in wave 2

- **12 entities** in `backend/src/SonrisaNews.Domain/Entities/` — the 10
  from `1-features.md` §4 (User, Channel, Alert, AlertChannelMode, Event,
  Match, Notification, Source, AuditLog) plus the three auth-token
  entities Wave 3 needs (EmailVerification, PasswordResetToken, RefreshToken).
  Wave 3's first migration is additive, not a "now we have to migrate the
  auth tables too" surprise.
- **12 `IEntityTypeConfiguration<T>`** classes in `backend/src/SonrisaNews.Infrastructure/Persistence/Configurations/`.
  Each dependent entity has its FK declared in its own configuration
  (no shadow Guid columns).
- **3 required composite indexes** per `1-features.md` §4:
  `IX_Events_SourceId_OccurredAt`, `IX_Matches_AlertId_FiredAt`,
  `IX_Notifications_UserId_SentAt`.
- **Schema tests** in `backend/tests/SonrisaNews.UnitTests/DatabaseSchemaTests.cs`:
  - All 12 entities are mapped.
  - User round-trips through SQLite `:memory:`.
  - AlertChannelMode has a composite PK on `(AlertId, ChannelId)`.
  - The 3 required composite indexes exist.
  - All 9 dependent-column FK relationships are declared.
  - Notification.Status round-trips as a typed enum.
  - RefreshToken + PasswordResetToken + EmailVerification are mapped as entities.
  - `MigrationFolder_ContainsTheInitialSchemaMigration` (the tripwire).
- **Registration tests** in
  `backend/tests/SonrisaNews.UnitTests/SonrisaNewsDbContextRegistrationTests.cs`:
  - Blank connection string resolves to the absolute repo-rooted dev path.
  - Relative connection string in `appsettings.json` resolves to the repo root,
    not the process CWD.
- **Path resolver** in `backend/src/SonrisaNews.Shared/SonrisaRepositoryPaths.cs`
  with `FindRepoRoot` and `ResolveDevSqliteFilePath`. The walker is bounded
  to 16 levels and looks for `AGENTS.md` or `.git` as a repo-root marker.
- **Design-time factory** in
  `backend/src/SonrisaNews.Infrastructure/Persistence/SonrisaNewsDbContextDesignTimeFactory.cs`
  so `dotnet ef` works without a running host.
- **Cleanup service stub** in `backend/src/SonrisaNews.Infrastructure/Background/CleanupService.cs`
  with a `BackgroundService` wrapper in `backend/src/SonrisaNews.Worker/CleanupBackgroundService.cs`.
  Logs "no retention rules applied in wave 2" and exits. **Real rules land in wave 8.**

## Schema decisions that need a future wave to make right

These are conscious deferrals. The implementer chose the simplest correct value
to get wave 2 green and let the future wave refine it.

### 1. FK `OnDelete` behaviour is uniform right now (wave 8 may need to vary)

Wave 2's `OnDelete` choices are:

| FK | Behaviour | Why |
|---|---|---|
| `Match.AlertId`, `Match.EventId` | `Restrict` | A match is a permanent audit record; can't lose the alert or event that fired it. |
| `Event.SourceId` | `Restrict` | The matcher's window query relies on Event rows sticking around; can't lose the source. |
| `Alert.UserId`, `Channel.UserId` | `Restrict` | The user-owns-alert and user-owns-channel invariants are core to RBAC. |
| `Notification.MatchId` | `Restrict` | Activity feed is read-only after send. |
| `EmailVerification.UserId`, `PasswordResetToken.UserId`, `RefreshToken.UserId` | `Cascade` | These are short-lived auth artifacts; on user delete they should be cleaned up. |
| `AlertChannelMode.AlertId`, `AlertChannelMode.ChannelId` | `Cascade` | Join rows are useless without both parents. |

If a future wave needs different behaviour (e.g. wave 8 cleanup might want
`Cascade` on `Channel.UserId` so a deleted user loses their channels, not just
orphans them), the change is a single `OnDelete(...)` flip in the
configuration + a new migration. The test
`DbContext_DeclaresForeignKey_OnDependentColumn` only checks the FK is
declared, not the `OnDelete` value, so it's a low-friction change.

### 2. `EmailVerification.Token` and `PasswordResetToken.Token` store plaintext

The wave 2 review flagged these as a vulnerability pattern: tokens stored in
plaintext, indexed as unique, means anyone with DB read access can list every
active email-verification or password-reset link.

`RefreshToken` already uses `TokenHash` (the plaintext lives in the
httpOnly cookie). `EmailVerification` and `PasswordResetToken` should follow
the same pattern — but **the column shape is exactly the same as the
plaintext version, the difference is the *value* the auth controller writes
in wave 3**. The wave-3 auth controller should hash before insert (Argon2id
or a faster HMAC-SHA256, since these tokens are short-lived by design).

**Action in wave 3**: when implementing `AuthController.ForgotPassword` and
`AuthController.VerifyEmail`, hash the generated token with the same
`PasswordHasher<TUser>` (or a similar one-shot hash) and store the hash.
The plaintext is only in the email body. The schema doesn't need to change.

If for some reason the wave-3 implementer wants the column to be named
`TokenHash` to make the storage intent clear at the entity level, that's a
small `IEntityTypeConfiguration<>` rename + a new migration. NIT, not required.

### 3. `Source.Type` and `Event.Type` are independent

`Event.Type` reuses `AlertType` (News / Market / Disaster). A `Source` also
has a `Type` (`SourceType`: News / MarketSymbol / Disaster). The two are
*not* enforced to match at the schema level — a poller can write a `News`
event into a `MarketSymbol` source, and the matcher has to filter it out.

**Action in wave 6**: add the runtime invariant `Event.Type == Source.Type`
in `INewsDataSource.FetchAsync` and `IMarketDataSource.FetchAsync`. The
matcher (`IEventMatcher`) should also assert this when matching. The schema
stays the same.

If a future wave wants a SQL check constraint, the right way to add it is:

```csharp
migrationBuilder.Sql(
    "CREATE TRIGGER trg_event_type_matches_source " +
    "BEFORE INSERT ON Events " +
    "FOR EACH ROW WHEN NEW.Type != (SELECT Type FROM Sources WHERE Id = NEW.SourceId) " +
    "BEGIN SELECT RAISE(ABORT, 'Event.Type must match Source.Type'); END;");
```

…but SQLite triggers don't port to Postgres — Postgres uses `CHECK`
constraints with a subquery, which historically have had planner issues.
Don't add a trigger until the cross-DB port is solved (post-wave-3 once the
Postgres migration is concrete). NIT for now.

### 4. `CleanupService` swallows all non-cancellation exceptions

```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Cleanup pass failed");
}
```

Wave 2's stub can't fail in any meaningful way (it just logs and returns).
**Wave 8 must add**:
- A counter exposed on the admin health page (e.g. `cleanup_consecutive_failures`).
- A circuit breaker that flips the worker to a "degraded" state after N
  consecutive failures.
- Possibly a dead-letter row in `AuditLog` so the failure is visible in the
  admin UI even when the worker host is down.

The wave-2 reviewer flagged this as "the comment in the code says 'Wave 8
will route this through the admin health page' — should be a
`// TODO(sonrisa-news#N):` referencing the issue". Wave 8's first task is
to replace this comment with the real handler.

### 5. `CleanupBackgroundService` constructs `CleanupService` manually with a `TypedForwarder`

```csharp
var serviceLogger = new TypedForwarder(logger);
var cleanup = new CleanupService(serviceLogger);
```

This is a wave-2 smell: the worker host reaches into Infrastructure to
construct a service that takes only an `ILogger<T>`. Wave 8's real
`CleanupService` constructor will likely need an `IDbContextFactory<SonrisaNewsDbContext>`
or `IServiceScopeFactory`, and at that point the right move is to define
an `IRetentionPolicy` interface in `SonrisaNews.Domain` and have the worker
resolve it from DI:

```csharp
public interface IRetentionPolicy
{
    Task RunOnceAsync(CancellationToken cancellationToken);
}
```

**Action in wave 8**: extract the interface, register the implementation in
`AddSonrisaNewsInfrastructure`, and have `CleanupBackgroundService.ExecuteAsync`
inject `IRetentionPolicy` via the primary constructor. The `TypedForwarder`
hack goes away.

### 6. `Notification.Error` is `string?` and not a structured `ErrorCode + ErrorDetail`

`Notification.Error` stores the last delivery error message. The wave-2
review noted that an enum-style `NotificationErrorCode` (e.g.
`SmtpTimeout`, `SlackRateLimited`, `ChannelUnverified`, `RecipientRejected`)
plus a free-form `ErrorDetail` string would let the activity feed show
"rejected" vs "timed out" badges without parsing the message.

**Action in wave 8 (dispatcher)**: when implementing `INotificationChannel.SendAsync`
and writing the failure to `Notification.Error`, also write a
`NotificationErrorCode` to a new column. Adding the column is a new migration
forward — easy to do once the dispatcher lands. The current `string? Error`
is a placeholder, not a constraint on the design.

### 7. `Channel.QuietHoursStart/End` are `TimeOnly?` without explicit column type

SQLite stores them as `TEXT` of format `HH:mm:ss.fffffff`. Postgres will
default to `text` too unless we add `HasColumnType("time")`. The MVP round-trip
works; the wave-8 timezone work (rendering quiet-hours in the user's local
time) will need the explicit column type for the Postgres swap to use the
native `time with time zone` type.

**Action in wave 8**: in `ChannelConfiguration`, add
`builder.Property(c => c.QuietHoursStart).HasColumnType("TEXT");` and
`.HasColumnType("text")` on the corresponding Postgres-specific
configuration (when the swap happens).

### 8. `Worker/Program.cs` registers cleanup AFTER the heartbeat `SonrisaWorker`

```csharp
builder.Services.AddHostedService<SonrisaNews.Worker.SonrisaWorker>();      // heartbeat
builder.Services.AddHostedService<SonrisaNews.Worker.CleanupBackgroundService>();  // cleanup
```

The visual order says "heartbeat is primary, cleanup is secondary". In wave 8
the cleanup loop is the more important service (it enforces the retention
rules). Move the registration order and add an XML doc comment explaining why.

### 9. `Microsoft.Extensions.Http.Resilience` was removed from `SonrisaNews.Infrastructure.csproj`

It was a premature dependency (no `HttpClient` is constructed in wave 2). Wave 6
(yfinance sidecar client) and wave 8 (dispatcher retries to Slack) will need
Polly v8 retry policies. **Re-add it then** in the PR that adds the first
`HttpClient` use, so the dependency is added alongside the use.

### 10. `appsettings.Development.json` is duplicated across all three projects

`backend/src/SonrisaNews.{Api,Worker,Infrastructure}/appsettings.Development.json`
all contain the same connection string. The `Infrastructure` copy exists only
to feed the EF design-time factory. This is fine, but a `README.md` note in
`backend/src/SonrisaNews.Infrastructure/` explaining "this appsettings is read
by `dotnet ef`, not by the runtime hosts" would help the next agent. NIT.

### 11. `SonrisaRepositoryPaths.ResolveSqliteDataSource` consolidation

`SonrisaNewsDbContextRegistration.cs` and `SonrisaNewsDbContextDesignTimeFactory.cs`
both contain near-duplicate "is the connection string relative? resolve it
against the repo root" logic, with slightly different fallback behaviour
(blank-config throws, configured-relative falls back to CWD). They should
share a single helper:

```csharp
public static string ResolveSqliteDataSource(string? connectionString, string? startDirectory);
```

**Action**: small refactor PR after wave 3 lands. The two test files
(`SonrisaRepositoryPathsTests`, `SonrisaNewsDbContextRegistrationTests`)
together cover the behaviour, so the refactor is safe to do with both tests
green.

### 12. The wave-1/2 `CleanupBackgroundService` constructor uses an older host signature

The `Worker/Program.cs` registers `CleanupBackgroundService` as a
`BackgroundService` via `AddHostedService<>` — this works in .NET 8+. No
change needed; the handoff note is just so wave 8 doesn't accidentally
register it twice (once as the concrete class, once as a wrapper).

---

## Items added by the wave-2 reviewer (deferred for the same reason)

These are findings from the Backend Reviewer that didn't make wave 2 but
are small enough to batch into a future wave. Each is in the "should fix,
not blocking" tier.

### 13. `CleanupService.RunOnceAsync` is `async`-shaped but synchronous

```csharp
public Task RunOnceAsync(CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    _logger.LogInformation("…");
    return Task.CompletedTask;
}
```

The body never `await`s anything; the `async` keyword is the right shape
for the wave-8 implementation, but right now it's just noise. Future agents
copy this pattern and propagate it. The wave-2 reviewer's nit: drop the
`async` keyword and let the method be `public Task RunOnceAsync(...) =>
Task.CompletedTask;` until wave 8 makes it actually await something.

**Action**: 1-line cleanup; do it in the wave that touches `CleanupService`
(wave 8).

### 14. `Match` FKs have no navigation properties on the principal side

`MatchConfiguration` declares `HasOne<Alert>().WithMany()` and
`HasOne<Event>().WithMany()` with no `.WithMany(a => a.Matches)`. The
schema is correct (shadow FK with `ON DELETE RESTRICT` and an automatic
index on the dependent side), but the wave-6 matcher will probably want
to walk `alert.Matches` for the idempotency check and right now has to
write a manual join.

**Action in wave 6**: add `ICollection<Match> Matches { get; set; } = [];`
to `Alert.cs` and `Event.cs`, switch the configurations to
`HasOne<Alert>().WithMany(a => a.Matches)`. If wave 6 doesn't end up using
them, dead-code removal later.

### 15. Entities don't override `Equals` / `GetHashCode` or use `record`

All 12 entities are reference types. EF Core tracks them by `Id` inside a
single DbContext, which works. But cross-context comparisons
(admin tool reading from a snapshot, API reading from the live store) will
see two `Alert` instances with the same `Id` as different by reference.
The wave-5 alert CRUD controller's `if (existingAlert.Name != dto.Name)`
check will silently miss updates.

**Action in wave 5 (or earlier if convenient)**: add
`Equals`/`GetHashCode` based on `Id`, or migrate the entities to
`record class`. The codebase already uses `record` for DTOs per
`csharp-dotnet.instructions.md`, so the entity refactor is consistent.
One PR, all 12 entities.

### 16. `MigrationFolder_ContainsTheInitialSchemaMigration` is a substring match

The test does `File.ReadAllText(initialSchemaMigrations[0]).Should()
.Contain("Forward-only after merge", …)`. If a future agent inserts a line
above the comment (e.g. `var stopwatch = Stopwatch.StartNew();` to time
the migration), the tripwire still passes — `Contains` is substring-only.
The rule in `database-migrations.instructions.md` is "the first line of
`Up`", not "anywhere in the file".

**Action in wave 3+**: when the second migration lands, harden the
tripwire to look at the first non-XML-doc line of each `Up` method
(regex `^\s*// Forward-only after merge\.` immediately after the
`protected override void Up(...)` brace). Rename the test to
`MigrationFolders_AllMigrationsDeclareForwardOnlyComment` and assert
on every `*_*.cs` file in the folder, not just `InitialSchema`.

### 17. `System.Security.Cryptography.Xml` pinned at 10.0.6, rest of stack at 10.0.0

`SonrisaNews.Infrastructure.csproj` references
`System.Security.Cryptography.Xml 10.0.6`, six patch levels higher than
the rest of the .NET 10.0.0 stack. No wave-2 code uses it (the only
crypto is the `PasswordHash` column, which Argon2id libs handle). The
version pin is suspicious — was there a CVE? If intentional, comment why;
if not, align with the rest of the stack.

**Action in wave 3**: either align the version or add a comment in the
csproj explaining the pin. 30 seconds of work either way.

### 18. No global query filter for `User.DeletedAt IS NULL`

Wave 2 has the `User.DeletedAt` column but no automatic filter. The
wave-8 cleanup service is the right place to add
`modelBuilder.Entity<User>().HasQueryFilter(u => u.DeletedAt == null)` —
admin endpoints will then need `.IgnoreQueryFilters()` to see deleted
users. The wave-10 admin "users" page will land this naturally.

**Action in wave 8 (cleanup)**: add the filter when the cleanup service
implements the 30-day grace period. Document which admin endpoints need
`.IgnoreQueryFilters()` in the wave-10 handoff.

### 19. `Cleanup pass failed` log line doesn't include the run timestamp

`CleanupBackgroundService` catches `Exception ex` and logs
`logger.LogError(ex, "Cleanup pass failed")` — no `RunAtUtc` property.
Future wave-8 health-page integration will want to grep logs by run time
and won't be able to. The fix is one line:
`logger.LogError(ex, "Cleanup pass failed at {RunAtUtc}", clock.UtcNow)`.
But wave 2's stub doesn't take a clock; wave 8 widens the
`CleanupBackgroundService` constructor anyway, so add the property then.

**Action in wave 8**: replace this catch with the structured
`{RunAtUtc}` form when wave 8 wires the loop to `IClock`.

### 20. `MaxWalkDepth = 16` has no inline comment

`SonrisaRepositoryPaths.cs` declares `private const int MaxWalkDepth = 16;`
with no comment on the literal. The XML doc above mentions "a malformed
CWD does not loop forever" but doesn't justify 16. Normal Windows paths
are 8-10 levels deep; 16 is generous to allow for monorepo or container
nesting.

**Action in any wave that touches `SonrisaRepositoryPaths`**: add
`// 8 deep on stock Windows + 8 for monorepo or container nesting.` as an
inline comment on the constant. Trivial; do it next time the file is open.

### 21. `UserStatus.SoftDeleted = 4` vs `User.DeletedAt` — two different mechanisms

The enum value `UserStatus.SoftDeleted = 4` exists in the model but is not
written by any wave-2 code. The `User.DeletedAt` timestamp is the
wave-8 cleanup signal; the status enum is the API surface. They're
complementary, not redundant: a user can be `Status = SoftDeleted` (visible
to admins on the wave-10 "users" page) while `DeletedAt` is the actual
hard-delete cutoff used by cleanup. Document the distinction so wave-10
admin doesn't conflate them.

**Action in wave 10 (admin)**: when implementing the user-management
page, add a one-line XML doc on `UserStatus.SoftDeleted` clarifying that
it's the API-level marker, not the cleanup signal.

---

## How to re-scaffold the migration after future schema changes

Per `database-migrations.instructions.md`:

```powershell
# If the model changes (entity, configuration, FK, index, enum):
cd C:\Users\Lenovo\repos\SonrisaNews
dotnet ef migrations add <WaveN_ChangeName> -p:backend/src/SonrisaNews.Infrastructure
dotnet ef database update -p:backend/src/SonrisaNews.Infrastructure
dotnet test backend/SonrisaNews.slnx
```

Never hand-edit `backend/**/Migrations/*.cs` after the `// Forward-only after merge.`
marker. The first line of `Up` must always be that comment (the
`MigrationFolder_ContainsTheInitialSchemaMigration` test enforces it for
`InitialSchema`; a similar test will be needed for subsequent migrations).

## How to verify the wave-2 work is intact

```powershell
cd C:\Users\Lenovo\repos\SonrisaNews
dotnet test backend/SonrisaNews.slnx
```

Expect: **32 passed, 0 failed, 0 skipped**, 0 warnings. The `--filter
Category=Database` subset is 11 tests (the schema, registration, and path
walker tests); the full suite also includes the cleanup-stub and enum tests
in the `Cleanup` category and the pre-existing `DomainExceptionTests` /
`EnumsTests` from wave 1.

## What the next wave should add

Wave 3 (auth) will land:
- `UserManager<TUser>` / `SignInManager<TUser>` registration against `User`.
- `AuthController` with signup/signin/refresh/verify/forgot/reset.
- `rbac_policy.csv` with the seed `p` and `g` lines.
- The token-hashing concern from item 2 above.
- The connection-string source logging (so the dev sees which
  `appsettings.json` won).

The wave-2 reviewer flagged that the wave-3 implementer should:
- Add a `LogInformation` of the resolved SQLite path in
  `AddSonrisaNewsDbContext` so the dev sees which file they ended up on.
- Hash `EmailVerification.Token` and `PasswordResetToken.Token` before insert.
- Decide the `OnDelete` behaviour for the User-Cascade-on-Channel question
  (currently `Restrict`; wave 3 might want `Cascade` to match the
  `RefreshToken` behaviour).
- Harden the `MigrationFolder_Contains…` tripwire to regex-match the
  first non-XML-doc line of every `Up` method (item **#16** above). The
  test name should change to `MigrationFolders_AllMigrationsDeclareForwardOnlyComment`.
- Resolve the `System.Security.Cryptography.Xml 10.0.6` pin in
  `SonrisaNews.Infrastructure.csproj` (item **#17** above). Either
  align with the rest of the stack or comment why it's pinned.
