using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// The current request's authenticated user. Backed by the JWT claims set
/// populated by <c>JwtBearer</c> middleware. The auth flow writes the
/// user's id (sub) and email into the token; this abstraction surfaces them
/// to controllers and the RBAC handler.
/// </summary>
/// <remarks>
/// The user's <b>role is intentionally not exposed here</b>. Per the
/// 2026-06-05 user rule, validation is always via the permission, never
/// via the role. Code that needs to know "may this user do X?" asks the
/// <c>RbacPolicyHandler</c> (which does the DB join) or, in tests, calls
/// the <c>Permissions</c> / <c>UserRoles</c> tables directly. There is no
/// <c>if (currentUser.Role == Admin)</c> path in this codebase.
/// </remarks>
public interface ICurrentUser
{
    /// <summary>The user's id, or <c>null</c> if the request is unauthenticated.</summary>
    Guid? Id { get; }

    /// <summary>The user's email, or <c>null</c> if the request is unauthenticated.</summary>
    string? Email { get; }

    /// <summary>True if the request has a valid JWT.</summary>
    bool IsAuthenticated { get; }
}

/// <summary>
/// <see cref="ICurrentUser"/> implementation backed by
/// <see cref="IHttpContextAccessor"/>. The JWT bearer middleware is
/// responsible for turning the bearer token into claims; we just read
/// them here.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private const string IdClaimType = ClaimTypes.NameIdentifier;
    private const string EmailClaimType = ClaimTypes.Email;

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public Guid? Id
    {
        get
        {
            var raw = FindClaim(IdClaimType);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => FindClaim(EmailClaimType);

    private string? FindClaim(string type)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null || !user.Identity?.IsAuthenticated == true) return null;
        return user.FindFirst(type)?.Value;
    }
}
