---
name: 'rbac-audit'
description: 'Runs an audit script that diffs the RBAC policy file against the [Authorize(Policy = ...)] attributes in the controllers. Reports any policy used in code that is not granted in the policy file, and any permission in the policy file that is not used.'
---

# RBAC Audit

A drift between the controllers' `[Authorize]` attributes and the `rbac_policy.csv` file is a security bug. Either:

- A policy is **granted in the CSV but not used** in code (dead permission — usually harmless, but a sign of incomplete work).
- A policy is **used in code but not granted** in the CSV (worst case: 403 to legitimate users, or 200 to illegitimate ones if the fallback is permissive).

The audit catches both.

## 1. The audit script

A small `dotnet` console app at `backend/tools/RbacAudit/Program.cs`. Run with:

```bash
dotnet run --project backend/tools/RbacAudit -- \
    --policies backend/src/SonrisaNews.Infrastructure/Auth/rbac_policy.csv \
    --controllers backend/src/SonrisaNews.Api
```

Or, in CI:

```yaml
- name: RBAC audit
  run: dotnet run --project backend/tools/RbacAudit -- --policies backend/src/SonrisaNews.Infrastructure/Auth/rbac_policy.csv --controllers backend/src/SonrisaNews.Api
```

## 2. The script's job

1. **Parse `rbac_policy.csv`**. Build a set of `p, <role>, <resource>, <action>` tuples and `g, <user>, <role>` tuples.
2. **Walk every controller in `--controllers`**. Use Roslyn (`Microsoft.CodeAnalysis.CSharp`) to find:
   - Every class with `[ApiController]` or `[Controller]`.
   - Every action with `[Authorize(Policy = "...")]`.
3. **For each policy used in code**, check that at least one role in the CSV grants it (or that the role is `*` and the resource is `*`).
4. **For each policy in the CSV**, check that it's used somewhere in code. If not, warn.
5. **Print a report** to stdout. Exit non-zero on any "missing" finding; warn (exit 0) on "unused".

## 3. The output format

```
RBAC Audit Report
=================

✅ Granted policies used in code:
  - Alerts.Read.Own      (User, Admin)
  - Alerts.Write.Own     (User, Admin)
  - Channels.Read.Own    (User, Admin)
  - Channels.Write.Own   (User, Admin)
  - Sources.Write.Any    (Admin)
  - Users.Suspend        (Admin)

⚠️  Granted policies not used in code:
  - AuditLog.Read        (Admin)            — no [Authorize(Policy = "AuditLog.Read")] found
  - Matcher.Run          (System)           — internal; not expected to be in controllers

❌ Used in code but NOT granted in policy file:
  - Foo.Bar.Baz          (controller: AdminAnnouncementsController.Send)

Exit code: 1 (fix the ❌ line)
```

## 4. The script's source

```csharp
// Simplified outline. Real implementation lives in the repo.
using System.CommandLine;            // for the CLI args
using Microsoft.CodeAnalysis;        // Roslyn
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var policiesArg = new Option<string>("--policies") { IsRequired = true };
var controllersArg = new Option<string>("--controllers") { IsRequired = true };

var root = new RootCommand { policiesArg, controllersArg };
root.SetHandler((policiesPath, controllersPath) =>
{
    var granted = ParsePolicyFile(policiesPath);
    var used = ScanControllers(controllersPath);

    var missing = used.Keys.Except(granted.Keys).ToList();
    var unused = granted.Keys.Except(used.Keys).ToList();

    // ... print the report ...
    Environment.Exit(missing.Count == 0 ? 0 : 1);
}, policiesArg, controllersArg);

return root.Invoke(args);

// ... ParsePolicyFile and ScanControllers implementations follow.
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
            if (!usages.ContainsKey(policyName))
                usages[policyName] = new List<string>();
            usages[policyName].Add(Path.GetFileName(file));
        }
    }

    return usages;
}
```

## 6. The policy file parser

The CSV is small and predictable. A line-based parse is enough:

```csharp
static Dictionary<string, List<string>> ParsePolicyFile(string path)
{
    var lines = File.ReadAllLines(path);
    var granted = new Dictionary<string, List<string>>();

    foreach (var line in lines)
    {
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
            continue;

        var parts = line.Split(',').Select(p => p.Trim()).ToArray();
        if (parts.Length < 4 || parts[0] != "p") continue;

        var role = parts[1];
        var resource = parts[2];
        var action = parts[3];
        var key = $"{resource}.{action}";

        if (!granted.ContainsKey(key))
            granted[key] = new List<string>();
        granted[key].Add(role);
    }

    return granted;
}
```

## 7. CI integration

The audit runs in the `backend` job in `ci.yml`. A failure blocks the PR.

The output is uploaded as a build artifact (`rbac-audit.txt`) so reviewers can see it without re-running the script.

## 8. When to run by hand

- After any change to `rbac_policy.csv`.
- After adding a new `[Authorize(Policy = "...")]` attribute.
- After adding a new permission constant to `Permissions.cs`.
- When a new controller is added.

## 9. Forbidden

- Editing the policy file to silence the audit (the audit exists *because* people forget to update the file)
- Marking the audit as a soft warning (it is a hard fail in CI)
- Adding a new policy to code without a corresponding `p` line
- Adding a new `p` line without updating the controllers (or marking it as "internal" with a comment)
