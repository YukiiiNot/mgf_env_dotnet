namespace MGF.DevSecretsCli;

using System.Text.Json;
using System.Text.Json.Nodes;

internal static class DevSecretsCommands
{
    private const string DefaultRequiredRelativePath = "tools/dev-secrets/secrets.required.json";
    private const string DefaultExportFileName = "dev-secrets.export.json";
    private const string DevConfigRelativePath = "config/appsettings.Development.json";
    private const string DevConfigSampleRelativePath = "config/appsettings.Development.sample.json";

    public static async Task<int> ExportAsync(
        string? outPath,
        string? requiredPath,
        bool verbose,
        CancellationToken cancellationToken)
    {
        try
        {
            var repoRoot = RepoLocator.FindRepoRoot();
            var required = SecretsRequiredConfig.Load(ResolveRequiredPath(requiredPath, repoRoot));
            var configPath = ResolveDevConfigPath(repoRoot);

            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine(
                    "devsecrets: config/appsettings.Development.json not found. Run devsecrets import to create it.");
                return 1;
            }

            var configRoot = LoadJsonObject(configPath);
            var flat = FlattenJson(configRoot);
            var filter = SecretsFilter.Filter(flat, required);

            if (filter.MissingRequired.Count > 0)
            {
                Console.Error.WriteLine(
                    $"devsecrets: missing required keys in appsettings.Development.json: {string.Join(", ", filter.MissingRequired)}");
                return 1;
            }

            if (filter.SkippedKeys.Count > 0)
            {
                Console.WriteLine(
                    $"devsecrets: warning skipped disallowed keys: {string.Join(", ", filter.SkippedKeys)}");
            }

            var export = new SecretsExportFile
            {
                SchemaVersion = 2,
                ToolVersion = VersionHelper.GetToolVersion(),
                ExportedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RepoCommit = await GitHelper.TryGetCommitAsync(repoRoot, cancellationToken),
                Secrets = filter.Allowed
            };

            var targetPath = ResolveOutputPath(outPath, repoRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? ".");

            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(targetPath, json, cancellationToken);
            Console.WriteLine($"devsecrets: wrote {targetPath}");

            if (verbose && export.Secrets.Count > 0)
            {
                foreach (var key in export.Secrets.Keys)
                {
                    Console.WriteLine($"devsecrets: export key {key}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"devsecrets: export failed: {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> ImportAsync(
        string inputPath,
        string? requiredPath,
        bool dryRun,
        bool verbose,
        bool force,
        CancellationToken cancellationToken)
    {
        try
        {
            var repoRoot = RepoLocator.FindRepoRoot();
            var required = SecretsRequiredConfig.Load(ResolveRequiredPath(requiredPath, repoRoot));
            var export = await SecretsExportFile.LoadAsync(inputPath, cancellationToken);

            var validation = SecretsFilter.ValidateExport(export, required);
            if (!validation.IsValid)
            {
                Console.Error.WriteLine($"devsecrets: validation failed: {validation.Error}");
                return 1;
            }

            var configPath = ResolveDevConfigPath(repoRoot);
            var samplePath = ResolveDevSamplePath(repoRoot);
            var targetExists = File.Exists(configPath);
            var sampleExists = File.Exists(samplePath);

            var configRoot = targetExists
                ? LoadJsonObject(configPath)
                : (sampleExists ? LoadJsonObject(samplePath) : new JsonObject());

            var merge = MergeSecrets(configRoot, export.Secrets, force);

            if (merge.SkippedKeys.Count > 0)
            {
                Console.WriteLine(
                    $"devsecrets: skipped existing keys (use --force to overwrite): {string.Join(", ", merge.SkippedKeys)}");
            }

            if (verbose && merge.SetKeys.Count > 0)
            {
                foreach (var key in merge.SetKeys)
                {
                    Console.WriteLine($"devsecrets: set {key}");
                }
            }

            if (dryRun)
            {
                Console.WriteLine("devsecrets: dry-run complete (no files written).");
                return 0;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? ".");
            WriteJson(configPath, configRoot);

            if (!targetExists && sampleExists)
            {
                Console.WriteLine("devsecrets: created appsettings.Development.json from sample.");
            }

            Console.WriteLine($"devsecrets: wrote {configPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"devsecrets: import failed: {ex.Message}");
            return 1;
        }
    }

    public static Task<int> ValidateAsync(
        string? requiredPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var repoRoot = RepoLocator.FindRepoRoot();
            var required = SecretsRequiredConfig.Load(ResolveRequiredPath(requiredPath, repoRoot));
            var configPath = ResolveDevConfigPath(repoRoot);

            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine("devsecrets: config/appsettings.Development.json not found.");
                return Task.FromResult(1);
            }

            var configRoot = LoadJsonObject(configPath);
            var flat = FlattenJson(configRoot);
            var filter = SecretsFilter.Filter(flat, required);

            if (filter.MissingRequired.Count > 0)
            {
                Console.Error.WriteLine("devsecrets: missing required keys:");
                foreach (var key in filter.MissingRequired)
                {
                    Console.Error.WriteLine($"  - {key}");
                }

                return Task.FromResult(1);
            }

            Console.WriteLine("devsecrets: required keys are present.");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"devsecrets: validate failed: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    private static string ResolveRequiredPath(string? requiredPath, string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(requiredPath))
        {
            return Path.IsPathRooted(requiredPath)
                ? Path.GetFullPath(requiredPath)
                : Path.GetFullPath(Path.Combine(repoRoot, requiredPath));
        }

        return Path.Combine(repoRoot, DefaultRequiredRelativePath);
    }

    private static string ResolveOutputPath(string? outPath, string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(outPath))
        {
            return Path.IsPathRooted(outPath)
                ? Path.GetFullPath(outPath)
                : Path.GetFullPath(Path.Combine(repoRoot, outPath));
        }

        return Path.GetFullPath(Path.Combine(repoRoot, DefaultExportFileName));
    }

    private static string ResolveDevConfigPath(string repoRoot)
        => Path.Combine(repoRoot, DevConfigRelativePath);

    private static string ResolveDevSamplePath(string repoRoot)
        => Path.Combine(repoRoot, DevConfigSampleRelativePath);

    private static JsonObject LoadJsonObject(string path)
    {
        var json = File.ReadAllText(path);
        var node = JsonNode.Parse(json) as JsonObject;
        return node ?? new JsonObject();
    }

    private static IReadOnlyDictionary<string, string> FlattenJson(JsonObject root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        FlattenNode(root, null, result);
        return result;
    }

    private static void FlattenNode(JsonNode? node, string? prefix, IDictionary<string, string> result)
    {
        if (node is null)
        {
            return;
        }

        if (node is JsonObject obj)
        {
            foreach (var entry in obj)
            {
                var nextPrefix = string.IsNullOrWhiteSpace(prefix)
                    ? entry.Key
                    : $"{prefix}:{entry.Key}";
                FlattenNode(entry.Value, nextPrefix, result);
            }

            return;
        }

        if (node is JsonValue value && !string.IsNullOrWhiteSpace(prefix))
        {
            result[prefix] = value.ToString();
        }
    }

    private static MergeResult MergeSecrets(JsonObject root, IReadOnlyDictionary<string, string> secrets, bool force)
    {
        var setKeys = new List<string>();
        var skippedKeys = new List<string>();

        foreach (var pair in secrets)
        {
            var existing = TryGetValue(root, pair.Key);
            if (!force && !string.IsNullOrWhiteSpace(existing))
            {
                skippedKeys.Add(pair.Key);
                continue;
            }

            SetValue(root, pair.Key, pair.Value);
            setKeys.Add(pair.Key);
        }

        return new MergeResult(setKeys, skippedKeys);
    }

    private static string? TryGetValue(JsonObject root, string key)
    {
        var current = (JsonNode?)root;
        var parts = key.Split(':', StringSplitOptions.None);

        foreach (var part in parts)
        {
            if (current is not JsonObject obj)
            {
                return null;
            }

            if (!obj.TryGetPropertyValue(part, out current))
            {
                return null;
            }
        }

        return current is JsonValue value ? value.ToString() : null;
    }

    private static void SetValue(JsonObject root, string key, string value)
    {
        var parts = key.Split(':', StringSplitOptions.None);
        var current = root;

        for (var index = 0; index < parts.Length - 1; index++)
        {
            var part = parts[index];
            if (!current.TryGetPropertyValue(part, out var node) || node is not JsonObject obj)
            {
                var next = new JsonObject();
                current[part] = next;
                current = next;
                continue;
            }

            current = obj;
        }

        current[parts[^1]] = value;
    }

    private static void WriteJson(string path, JsonObject root)
    {
        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private sealed record MergeResult(IReadOnlyList<string> SetKeys, IReadOnlyList<string> SkippedKeys);
}
