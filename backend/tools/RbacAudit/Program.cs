using System.CommandLine;

// --- CLI args ------------------------------------------------------------------
var policiesOption = new Option<FileInfo>(
    "--policies",
    "Path to the rbac_policy.csv file that defines the policy grants.")
{
    IsRequired = true,
};

var controllersOption = new Option<DirectoryInfo>(
    "--controllers",
    "Directory that contains the .cs controllers to audit.")
{
    IsRequired = true,
};

var root = new RootCommand("Audit RBAC usage: every [Authorize(Policy = \"...\")] attribute in the controllers must be granted in the policy file.")
{
    policiesOption,
    controllersOption,
};

root.SetHandler(async (FileInfo policies, DirectoryInfo controllers) =>
{
    var exitCode = await AuditAsync(policies, controllers);
    Environment.Exit(exitCode);
}, policiesOption, controllersOption);

return await root.InvokeAsync(args);


// ---------------------------------------------------------------------------
// Implementation
// ---------------------------------------------------------------------------
static async Task<int> AuditAsync(FileInfo policiesFile, DirectoryInfo controllersDir)
{
    if (!policiesFile.Exists)
    {
        Console.Error.WriteLine($"[SKELETON] ❌ Policies file not found: {policiesFile.FullName}");
        return 2;
    }

    if (!controllersDir.Exists)
    {
        Console.Error.WriteLine($"[SKELETON] ❌ Controllers directory not found: {controllersDir.FullName}");
        return 2;
    }

    // Wave 1: skeleton. The `[SKELETON]` prefix on every line is intentional — it
    // makes the no-op nature visible in CI logs so nobody mistakes this for a
    // real audit. Wave 3 fills in the Casbin model load + per-policy grant lookup
    // AND removes the `[SKELETON]` prefix in the same commit.
    var policyLines = await File.ReadAllLinesAsync(policiesFile.FullName);
    var policyCount = policyLines.Count(line =>
        !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'));

    Console.WriteLine($"[SKELETON] ✔  Loaded {policyCount} policy lines from {policiesFile.Name}");
    Console.WriteLine($"[SKELETON] ✔  Scanned {Directory.GetFiles(controllersDir.FullName, "*.cs", SearchOption.AllDirectories).Length} controller files");
    Console.WriteLine("[SKELETON] ✔  No findings (skeleton mode — wave 3 wires the full audit).");
    Console.WriteLine("[SKELETON] ⚠  THIS TOOL IS CURRENTLY A NO-OP. Do not rely on its exit code for security.");

    return 0;
}