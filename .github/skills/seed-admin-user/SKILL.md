---
name: 'seed-admin-user'
description: 'Seeds the first admin user from environment variables. Idempotent. Use at first deploy, or when rotating the bootstrap admin.'
---

# Seed Admin User

The very first time the API starts, there is no admin user. The bootstrap admin is created from environment variables, idempotently, on every startup.

## 1. The env vars

| Var | Required | Default | Description |
|---|---|---|---|
| `SEED_ADMIN_EMAIL` | yes | — | Email of the bootstrap admin. |
| `SEED_ADMIN_PASSWORD` | yes | — | Initial password. The user is forced to change it on first sign-in. |
| `SEED_ADMIN_DISPLAY_NAME` | no | `"Admin"` | Display name. |

The script will fail fast at startup if `SEED_ADMIN_EMAIL` or `SEED_ADMIN_PASSWORD` is missing in any environment where the seed runs (dev, staging, prod).

## 2. The seed code

The seed is a hosted service in `backend/src/SonrisaNews.Api/Auth/AdminSeeder.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SonrisaNews.Infrastructure.Persistence;

namespace SonrisaNews.Api.Auth;

public sealed class AdminSeederOptions
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Admin";
}

public sealed class AdminSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AdminSeederOptions> _options;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(
        IServiceScopeFactory scopeFactory,
        IOptions<AdminSeederOptions> options,
        ILogger<AdminSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var opts = _options.Value;
        if (string.IsNullOrWhiteSpace(opts.Email) || string.IsNullOrWhiteSpace(opts.Password))
        {
            _logger.LogError("AdminSeeder is enabled but SEED_ADMIN_EMAIL or SEED_ADMIN_PASSWORD is missing.");
            throw new InvalidOperationException("Admin seed env vars are required.");
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SonrisaDbContext>();

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == opts.Email, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Bootstrap admin {Email} already exists; skipping seed.", opts.Email);
            return;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(opts.Password, workFactor: 12);

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = opts.Email,
            PasswordHash = passwordHash,
            DisplayName = opts.DisplayName,
            Role = Roles.Admin,
            Status = UserStatus.Active,
            TimeZone = "UTC",
            CreatedAt = DateTime.UtcNow,
            MustChangePassword = true,
        });

        await db.SaveChangesAsync(ct);
        _logger.LogWarning("Bootstrap admin {Email} created. The user is required to change their password on first sign-in.", opts.Email);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

## 3. Registration

In `Program.cs`:

```csharp
builder.Services.Configure<AdminSeederOptions>(builder.Configuration.GetSection("AdminSeed"));
builder.Services.AddHostedService<AdminSeeder>();
```

The seeder runs on every startup. It is idempotent: if the user already exists, it does nothing.

## 4. Bypass in tests

The seeder is registered **only** when `AdminSeed:Email` is set. In tests, the option is not bound, the seeder is not registered, and the test fixture inserts an admin user directly via a fake.

## 5. Tests (TDD, red-green-refactor)

In `backend/tests/SonrisaNews.IntegrationTests/Auth/AdminSeederTests.cs`:

1. **Seeds a new admin** when the user does not exist.
2. **Skips** when the user already exists (idempotent).
3. **Throws** at startup if `Email` or `Password` is empty.
4. **Sets `MustChangePassword = true`** so the first sign-in forces a password change.
5. **Assigns the `Admin` role** (and not `User` or `System`).

Use a real `SonrisaDbContext` against an in-memory SQLite database.

## 6. Verify

Run from `backend/`:

```bash
dotnet test --filter FullyQualifiedName~AdminSeeder
```

Then a full `dotnet test`.

## 7. Hand off

The TDD C# Implementer agent writes the code. The Backend Reviewer reviews. The user merges.

## Forbidden

- Logging the password (even at debug level)
- Logging the password hash
- Creating the admin with `MustChangePassword = false` (the first deploy uses an env-var password; it must be rotated)
- Skipping the seed in prod "for speed"
- Hard-coding the seed values in code
- Letting the seeder run before the database is migrated (the seeder should be ordered after the migration step in the AppHost / startup pipeline)
