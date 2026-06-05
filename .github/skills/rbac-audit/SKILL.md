---
name: 'rbac-audit'
description: 'Runs an audit script that diffs the [Authorize(Policy = ...)] attributes in the controllers against the permissions table in the DB. Reports any policy used in code that does not exist as a row in `Permissions`, and any row in `RolePermissions` that is not used by code.'
---

# RBAC Audit

A drift between the controllers' `[Authorize]` attributes and the DB-backed RBAC catalog is a security bug. The audit catches two cases:

- A permission is **used in code but not granted in the DB** (worst case: 403 to legitimate users, or 200 to illegitimate ones if the fallback is permissive).
- A permission is **granted in the DB but not used in code** (dead permission — usually harmless, but a sign of incomplete work).

The RBAC model is **DB-driven** (user rule, 2026-06-05): five tables — `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`. There is no CSV and no Casbin. The audit reads the DB directly.

## 1. The audit script

A small `dotnet` console app at `backend/tools/RbacAudit/Program.cs`. Run with:

```bash
# Default: reads from the dev SQLite DB at data/sonrisa.db.
dotnet run --project backend/tools/RbacAudit

# Or point at a specific DB:
dotnet run --project backend/tools/RbacAudit -- \
    --connection "Data Source=data/sonrisa.db" \
    --controllers backend/src/SonrisaNews.Api
```

Or, in CI:

```yaml
- name: RBAC audit
  run: dotnet run --project backend/tools/RbacAudit
```

The script **takes no policy file**. The DB is the policy.

## 2. The script's job

1. **Open the SQLite/Postgres DB** at the connection string from `ConnectionStrings:Sonrisa` (or `--connection`).
2. **Read the `Permissions` table** to build the set of permission names that exist in the catalog.
3. **Read `RolePermissions`** to build the set of `(role, permission)` grants.
4. **Walk every controller in `--controllers`** (default `backend/src/SonrisaNews.Api`). Use Roslyn (`Microsoft.CodeAnalysis.CSharp`) to find:
   - Every class with `[ApiController]` or `[Controller]`.
   - Every action with `[Authorize(Policy = "...")]`.
5. **For each policy used in code**, check that the permission exists in the `Permissions` table AND that at least one role in `RolePermissions` grants it.
6. **For each `(role, permission)` in `RolePermissions`**, check that the permission is used somewhere in code. If not, warn.
7. **Print a report** to stdout. Exit non-zero on any "missing" finding; warn (exit 0) on "unused".

## 3. The output format

```
RBAC Audit Report
=================

✅ Granted permissions used in code:
  - Alerts.Read.Own      (granted to: User, Admin)
  - Alerts.Write.Own     (granted to: User, Admin)
  - Channels.Read.Own    (granted to: User, Admin)
  - Channels.Write.Own   (granted to: User, Admin)
  - Sources.Write.Any    (granted to: Admin)
  - Users.Suspend        (granted to: Admin)

⚠️  Granted permissions not used in code:
  - AuditLog.Read        (Admin)            — no [Authorize(Policy = "AuditLog.Read")] found
  - Matcher.Run          (System)           — internal; not expected to be in controllers

❌ Used in code but NOT granted in DB:
  - Foo.Bar.Baz          (controller: AdminAnnouncementsController.Send)

Exit code: 1 (fix the ❌ line)
```

## 4. The script's source

```csharp
// Simplified outline. Real implementation lives in the repo.
using System.CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using SonrisaNews.Infrastructure.Persistence;

var connectionOption = new Option<string>("--connection") { IsRequired = false };
var controllersOption = new Option<string>("--controllers") { IsRequired = false };

var root = new RootCommand { connectionOption, controllersOption };
root.SetHandler(async (string? connection, string? controllers) =>
{
    var exitCode = await AuditAsync(connection, controllers);
    Environment.Exit(exitCode);
}, connectionOption, controllersOption);

return await root.InvokeAsync(args);

static async Task<int> AuditAsync(string? connectionString, string? controllersPath)
{
    connectionString ??= Environment.GetEnvironmentVariable("ConnectionStrings__Sonrisa")
        ?? "Data Source=data/sonrisa.db";
    controllersPath ??= "backend/src/SonrisaNews.Api";

    // 1. Open the DB and read the catalog
    using var db = new SonrisaNewsDbContext(
        new DbContextOptionsBuilder<SonrisaNewsDbContext>()
            .UseSqlite(connectionString).Options);

    var granted = await db.RolePermissions
        .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => new { p.Name })
        .Select(x => x.Name)
        .ToListAsync();

    // 2. Walk controllers via Roslyn (see §5)
    var used = ScanControllers(controllersPath);

    // 3. Report
    var missing = used.Keys.Except(granted).ToList();
    var unused = granted.Except(used.Keys).ToList();
    // ... print the report ...
    return missing.Count == 0 ? 0 : 1;
}
```

## 5. The Roslyn scan

The interesting bit is finding the `[Authorize(Policy = "...")]` attributes. Roslyn makes this easy:

```csharp
static Dictionary<string, List<string>> ScanControllers(string rootPath)
{
    var files = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories);
    var usages = new Dictionary<string, List<string>>();

    foreach (var file in files)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
        var root = tree.GetRoot();
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>()
            .Where(a => a.Name.ToString().Contains("Authorize"));

        foreach (var attr in attributes)
        {
            var policyArg = attr.ArgumentList?.Arguments
                .FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == "Policy");
            if (policyArg is null) continue;

            var policyName = policyArg.Expression.ToString().Trim('"');
            if (!usages.ContainsKey(policyName)) usages[policyName] = new List<string>();
            usages[policyName].Add(Path.GetFileName(file));
        }
    }

    return usages;
}
```

## 6. CI integration

The audit runs in the `backend` job in `ci.yml`. A failure blocks the PR.

The output is uploaded as a build artifact (`rbac-audit.txt`) so reviewers can see it without re-running the script.

## 7. When to run by hand

- After any migration that touches `Permissions` or `RolePermissions`.
- After adding a new `[Authorize(Policy = "...")]` attribute.
- After adding a new permission constant to `Permissions.cs` (the migration must seed the new row before the audit is run).
- When a new controller is added.

## 8. Forbidden

- **Editing the seed data to silence the audit** (the audit exists *because* people forget to add the grant)
- Marking the audit as a soft warning (it is a hard fail in CI)
- Adding a new policy to code without seeding the `Permissions` row + the `RolePermissions` grant in a migration
- Adding a new row to `Permissions` without using it in a controller (or marking it as "internal" with a comment)
- Re-introducing a Casbin / CSV policy file (the model is DB-driven; CSV was removed)
