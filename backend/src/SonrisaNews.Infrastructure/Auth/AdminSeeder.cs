using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Auth;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Persistence;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// Configuration for the bootstrap admin seeder. Bound from the
/// <c>AdminSeed</c> section of <c>appsettings.json</c> (which is
/// populated from <c>SEED_ADMIN_EMAIL</c> / <c>SEED_ADMIN_PASSWORD</c>
/// env vars via the API's <c>Program.cs</c>).
/// </summary>
public sealed class AdminSeederOptions
{
    /// <summary>Configuration section name. Used by the API's <c>Program.cs</c> binding.</summary>
    public const string SectionName = "AdminSeed";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Admin";
}

/// <summary>
/// Seeds the first admin user from environment variables. Idempotent:
/// if the user already exists, the seeder is a no-op. The seeder is
/// registered as an <see cref="IHostedService"/> so it runs on every
/// startup, after the migration step has brought the schema up to date.
/// </summary>
/// <remarks>
/// The MVP convention per the <c>seed-admin-user</c> skill:
/// <list type="bullet">
///   <item><c>SEED_ADMIN_EMAIL</c> and <c>SEED_ADMIN_PASSWORD</c> are required when the seed runs.</item>
///   <item>The seed is a hard fail at startup if either env var is missing — operators should know about an unset seed, not silently get a no-admin situation.</item>
///   <item>The seeded user gets <c>MustChangePassword = true</c> so the first sign-in forces a password rotation.</item>
///   <item>The seeded user gets a row in <c>UserRoles</c> linking to the <c>Admin</c> role (via <see cref="RolesCatalogSeed.AdminRoleId"/>). The role is the DB source of truth; no in-memory mirror.</item>
/// </list>
/// </remarks>
public sealed class AdminSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<AdminSeederOptions> options,
    ILogger<AdminSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.Email) || string.IsNullOrWhiteSpace(opts.Password))
        {
            // If the operator hasn't set the seed env vars, we fail loud
            // at startup. The alternative (silently running with no
            // admin) is worse — the operator won't notice until they try
            // to sign in.
            throw new InvalidOperationException(
                "AdminSeeder requires SEED_ADMIN_EMAIL and SEED_ADMIN_PASSWORD. " +
                "Set them via appsettings.json (AdminSeed:Email / AdminSeed:Password), " +
                "user-secrets, or environment variables.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SonrisaNewsDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var normalized = opts.Email.Trim().ToLowerInvariant();
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (existing is not null)
        {
            // Idempotency: if the user already exists, don't recreate
            // them, but DO ensure the Admin role grant is in place.
            // (Useful when a prior run failed between the user insert
            // and the UserRoles insert.)
            var hasAdmin = await db.UserRoles
                .AnyAsync(ur => ur.UserId == existing.Id && ur.RoleId == RolesCatalogSeed.AdminRoleId, ct);
            if (!hasAdmin)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = existing.Id,
                    RoleId = RolesCatalogSeed.AdminRoleId,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(ct);
                logger.LogWarning("Bootstrap admin {Email} existed without the Admin role; added it.", normalized);
            }
            else
            {
                logger.LogInformation("Bootstrap admin {Email} already exists; skipping seed.", normalized);
            }
            return;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            PasswordHash = passwordHasher.Hash(opts.Password),
            DisplayName = string.IsNullOrWhiteSpace(opts.DisplayName) ? "Admin" : opts.DisplayName.Trim(),
            TimeZone = "UTC",
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            MustChangePassword = true,   // The env-var password must be replaced before long-term use.
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = RolesCatalogSeed.AdminRoleId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "Bootstrap admin {Email} created with role Admin. " +
            "The seeded password is from SEED_ADMIN_PASSWORD; rotate it on first sign-in.",
            normalized);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
