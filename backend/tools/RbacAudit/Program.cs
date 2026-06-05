using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace SonrisaNews.Tools.RbacAudit;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var dbOption = new Option<FileInfo>(
            "--db",
            "Path to the SQLite database file (e.g. data/sonrisa.db).")
        {
            IsRequired = true,
        };

        var controllersOption = new Option<DirectoryInfo>(
            "--controllers",
            "Directory that contains the .cs controllers to audit.")
        {
            IsRequired = true,
        };

        var root = new RootCommand("Audit RBAC usage: every [Authorize(Policy = \"...\")] attribute in the controllers must exist as a row in the Permissions table, and every permission in the DB that no code uses is reported as orphan.")
        {
            dbOption,
            controllersOption,
        };

        root.SetHandler((FileInfo db, DirectoryInfo controllers) =>
        {
            var exitCode = AuditAsync(db, controllers).GetAwaiter().GetResult();
            Environment.Exit(exitCode);
        }, dbOption, controllersOption);

        return await root.InvokeAsync(args);
    }

    // -- Implementation ---------------------------------------------------

    private static readonly Regex AuthorizePolicyRegex = new(
        @"\[Authorize\s*\(\s*Policy\s*=\s*(?:""(?<p>[^""]+)""|Permissions\.(?<p>[A-Za-z0-9_]+))",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex PermissionsConstantsRegex = new(
        @"public\s+const\s+string\s+(?<name>[A-Za-z0-9_]+)\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.Compiled);

    private static async Task<int> AuditAsync(FileInfo dbFile, DirectoryInfo controllersDir)
    {
        if (!dbFile.Exists)
        {
            Console.Error.WriteLine($"❌ DB file not found: {dbFile.FullName}");
            return 2;
        }

        if (!controllersDir.Exists)
        {
            Console.Error.WriteLine($"❌ Controllers directory not found: {controllersDir.FullName}");
            return 2;
        }

        var policyNamesInDb = await LoadPermissionNamesAsync(dbFile.FullName);
        var policyNamesInCode = LoadPolicyNamesInCode(controllersDir.FullName);

        var missingInDb = policyNamesInCode.Except(policyNamesInDb, StringComparer.OrdinalIgnoreCase).ToArray();
        var orphanInDb = policyNamesInDb.Except(policyNamesInCode, StringComparer.OrdinalIgnoreCase).ToArray();

        Console.WriteLine($"✔  Loaded {policyNamesInDb.Count} permissions from DB");
        Console.WriteLine($"✔  Scanned {Directory.GetFiles(controllersDir.FullName, "*.cs", SearchOption.AllDirectories).Length} controller files");
        Console.WriteLine($"✔  Found {policyNamesInCode.Count} [Authorize(Policy = ...)] references");

        var findings = 0;
        if (missingInDb.Length > 0)
        {
            findings += missingInDb.Length;
            Console.Error.WriteLine();
            Console.Error.WriteLine($"❌ {missingInDb.Length} polic{(missingInDb.Length == 1 ? "y" : "ies")} referenced in code are MISSING from the DB:");
            foreach (var name in missingInDb.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"   - {name}");
            }
        }

        if (orphanInDb.Length > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"⚠  {orphanInDb.Length} permission{(orphanInDb.Length == 1 ? "" : "s")} exist in the DB but no controller references them:");
            foreach (var name in orphanInDb.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"   - {name}");
            }
        }

        Console.WriteLine();
        if (findings > 0)
        {
            Console.Error.WriteLine($"❌ Audit FAILED with {findings} finding(s).");
            return 1;
        }

        Console.WriteLine("✔  Audit PASSED — every [Authorize(Policy)] is in the DB and no orphan permissions remain.");
        return 0;
    }

    private static async Task<HashSet<string>> LoadPermissionNamesAsync(string dbPath)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Permissions;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    private static HashSet<string> LoadPolicyNamesInCode(string controllersDir)
    {
        // Build a map of Permissions.<Identifier> → "Name" from the constants file,
        // so the audit can resolve Permissions.Foo to its string value.
        var permissionConstantsByIdentifier = new Dictionary<string, string>(StringComparer.Ordinal);
        // Walk up to the repo root and look for the Domain/Auth/Permissions.cs file.
        var dir = new DirectoryInfo(controllersDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src", "SonrisaNews.Domain", "Auth", "Permissions.cs");
            if (File.Exists(candidate))
            {
                foreach (Match m in PermissionsConstantsRegex.Matches(File.ReadAllText(candidate)))
                {
                    permissionConstantsByIdentifier[m.Groups["name"].Value] = m.Groups["value"].Value;
                }
                break;
            }
            dir = dir.Parent;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(controllersDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in AuthorizePolicyRegex.Matches(text))
            {
                var token = m.Groups["p"].Value;
                if (token.Contains('.'))
                {
                    names.Add(token);
                }
                else if (permissionConstantsByIdentifier.TryGetValue(token, out var resolved))
                {
                    names.Add(resolved);
                }
                else
                {
                    // Unresolved identifier — surface it as a literal so the
                    // auditor sees the gap.
                    names.Add(token);
                }
            }
        }
        return names;
    }
}
