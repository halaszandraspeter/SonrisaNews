using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SonrisaNews.Domain.Auth;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// Wires the auth + RBAC services into DI. Called from the API
/// <c>Program.cs</c>; not used by the Worker (the worker has no
/// HttpContext).
/// </summary>
public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Adds DB-driven RBAC + JWT bearer + ASP.NET Core authorization +
    /// the application auth services (password hasher, token hasher, JWT
    /// issuer, refresh-token service, audit log, current-user).
    /// </summary>
    public static IServiceCollection AddSonrisaNewsAuth(this IServiceCollection services, IConfiguration configuration)
    {
        // --- Options ------------------------------------------------------------
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // --- HTTP / Auth plumbing ----------------------------------------------
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        // --- RBAC (DB-driven; user rule 2026-06-05) ----------------------------
        // The handler is a singleton: it caches the (userId, permission)
        // decision so the per-request cost is one DB hit on first access,
        // then O(1) for the rest of the process lifetime. The cache
        // self-invalidates at process restart (which is when role grants
        // change in the typical deploy).
        services.AddSingleton<IAuthorizationHandler, RbacPolicyHandler>();

        services.AddAuthorization(options =>
        {
            // Default policy: any action with [Authorize] must have an
            // authenticated user. The handler-level RbacPolicyHandler does
            // the per-permission check. The fallback policy matches
            // AuthController + HealthController, which are marked
            // [AllowAnonymous] explicitly.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        // --- Auth services ------------------------------------------------------
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddScoped<IAuthTokenService, JwtAuthTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IAuditLog, AuditLogService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddSingleton(TimeProvider.System);

        // --- Bootstrap admin (registers an IHostedService) --------------------
        // The seeder is bound to options by the caller's Program.cs
        // (AddSonrisaNewsAuth doesn't know the env-var names). Register
        // the hosted service only; the options binding is the caller's
        // responsibility.
        services.AddHostedService<AdminSeeder>();

        // --- JWT bearer middleware ---------------------------------------------
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // The actual JwtOptions instance is read once and copied
                // into the JwtBearer options. This is fine for a single
                // API instance; a multi-replica deployment with key
                // rotation would need a metadata endpoint, but that's
                // post-MVP.
                var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                };
            });

        return services;
    }

    /// <summary>
    /// Registers an authorization policy for each permission constant in
    /// <see cref="Permissions"/>. Controllers use
    /// <c>[Authorize(Policy = Permissions.X)]</c> and the
    /// <see cref="RbacPolicyHandler"/> enforces the permission against
    /// the DB (UserRoles ⨝ RolePermissions ⨝ Permissions).
    /// </summary>
    public static IServiceCollection AddSonrisaNewsPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // For each permission constant, add a policy that requires
            // the PermissionRequirement to be satisfied. The
            // RbacPolicyHandler reads the requirement and consults the
            // DB to decide.
            var permissions = new[]
            {
                Permissions.AlertsReadOwn, Permissions.AlertsWriteOwn,
                Permissions.AlertsReadAny, Permissions.AlertsWriteAny,
                Permissions.ChannelsReadOwn, Permissions.ChannelsWriteOwn,
                Permissions.SourcesReadAny, Permissions.SourcesWriteAny,
                Permissions.UsersReadAny, Permissions.UsersWriteAny,
                Permissions.UsersSuspend, Permissions.AuditLogRead,
                Permissions.AnnouncementsWrite, Permissions.HealthRead,
                Permissions.MatcherRun, Permissions.ProfileRead,
            };

            foreach (var permission in permissions)
            {
                options.AddPolicy(permission, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new PermissionRequirement(permission));
                });
            }
        });

        return services;
    }
}
