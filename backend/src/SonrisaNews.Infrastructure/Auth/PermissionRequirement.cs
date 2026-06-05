using Microsoft.AspNetCore.Authorization;
using SonrisaNews.Domain.Auth;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// Marker for the authorization requirement resolved by
/// <c>RbacPolicyHandler</c>. The string payload is the permission name (one
/// of the constants in <see cref="Permissions"/>).
/// </summary>
/// <remarks>
/// ASP.NET Core's authorization system needs a class implementing
/// <c>IAuthorizationRequirement</c> to be associated with a policy via
/// <c>AddPolicy(name, policy =&gt; policy.Requirements.Add(new PermissionRequirement(name)))</c>.
/// The DB-driven handler reads the requirement's <see cref="Permission"/>
/// and asks the <c>UserRoles ⨝ RolePermissions ⨝ Permissions</c> join
/// whether the current user has it (cached per process for the rest of
/// the lifetime).
/// </remarks>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
