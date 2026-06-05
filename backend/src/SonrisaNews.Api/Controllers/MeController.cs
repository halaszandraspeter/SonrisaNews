using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonrisaNews.Domain.Auth;
using SonrisaNews.Infrastructure.Auth;
using SonrisaNews.Infrastructure.Persistence;

namespace SonrisaNews.Api.Controllers;

/// <summary>Returns the current user's profile. The simplest way to verify a freshly-issued JWT works.</summary>
[ApiController]
[Route("api/v1/me")]
[Authorize(Policy = Permissions.ProfileRead)]
public class MeController(ICurrentUser currentUser, SonrisaNewsDbContext db) : ControllerBase
{
    /// <summary>Returns the caller's id and email. <c>200</c> means the JWT is valid and the user exists. The role is intentionally not returned; clients ask <c>GET /api/v1/me/permissions</c> for the granted permission set.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<MeResponse> Get()
    {
        return Ok(new MeResponse(
            currentUser.Id ?? System.Guid.Empty,
            currentUser.Email ?? string.Empty));
    }

    /// <summary>Returns the caller's permission set as a string array. The frontend can cache this in memory and re-fetch on role changes.</summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(PermissionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PermissionsResponse>> GetPermissions(CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        var granted = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (_, rp) => rp)
            .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Name)
            .Distinct()
            .ToListAsync(ct);

        return Ok(new PermissionsResponse(granted));
    }
}

/// <summary>Response shape for <see cref="MeController.Get"/>. The role is omitted on purpose (user rule 2026-06-05): validation happens against the permission catalog, not against role membership.</summary>
public sealed record MeResponse(Guid UserId, string Email);

/// <summary>Response shape for <see cref="MeController.GetPermissions"/>.</summary>
public sealed record PermissionsResponse(IReadOnlyList<string> Permissions);

