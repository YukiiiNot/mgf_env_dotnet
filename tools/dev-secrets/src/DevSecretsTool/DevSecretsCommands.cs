namespace MGF.Tools.DevSecrets;

using System.Text.Json;

internal static class DevSecretsCommands
{
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
            var dotnet = new DotnetUserSecrets();

            var export = new SecretsExportFile
            {
                SchemaVersion = 1,
                ToolVersion = VersionHelper.GetToolVersion(),
                ExportedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RepoCommit = await GitHelper.TryGetCommitAsync(repoRoot, cancellationToken)
            };

            var projects = OrderProjects(required.Projects);

            foreach (var project in projects)
            {
                if (string.IsNullOrWhiteSpace(project.UserSecretsId))
                {
                    Console.Error.WriteLine($"devsecrets: missing UserSecretsId for project {project.Name}");
                    return 1;
                }

                var list = await dotnet.ListAsync(project.UserSecretsId, cancellationToken);
                var filter = SecretsFilter.Filter(list, project, required.GlobalPolicy);

                var entry = new ProjectSecretsExport
                {
                    Name = project.Name,
                    Path = project.Path ?? string.Empty,
                    UserSecretsId = project.UserSecretsId,
                    Secrets = filter.Allowed
                };

                if (filter.MissingRequired.Count > 0)
                {
                    Console.Error.WriteLine(
                        $"devsecrets: missing required keys for {project.Name} ({project.UserSecretsId}): {string.Join(", ", filter.MissingRequired)}");
                    return 1;
                }

                if (filter.SkippedKeys.Count > 0)
                {
                    Console.WriteLine(
                        $"devsecrets: warning skipped disallowed keys for {project.Name}: {string.Join(", ", filter.SkippedKeys)}");
                }

                export.Projects.Add(entry);

                Console.WriteLine($"devsecrets: {project.Name} exported {entry.Secrets.Count} keys");

                if (verbose && entry.Secrets.Count > 0)
                {
                    foreach (var key in entry.Secrets.Keys)
                    {
                        Console.WriteLine($"devsecrets: {project.Name} key {key}");
                    }
                }
            }

            var targetPath = ResolveOutputPath(outPath, repoRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? ".");

            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(targetPath, json, cancellationToken);
            Console.WriteLine($"devsecrets: wrote {targetPath}");
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
        CancellationToken cancellationToken)
    {
        try
        {
            var repoRoot = RepoLocator.FindRepoRoot();
            var required = SecretsRequiredConfig.Load(ResolveRequiredPath(requiredPath, repoRoot));
            var export = await SecretsExportFile.LoadAsync(inputPath, cancellationToken);

            var dotnet = new DotnetUserSecrets();
            foreach (var project in export.Projects)
            {
                var spec = required.FindByUserSecretsId(project.UserSecretsId);
                if (spec is null)
                {
                    Console.Error.WriteLine($"devsecrets: export contains unknown UserSecretsId {project.UserSecretsId}");
                    return 1;
                }

                var validation = SecretsFilter.ValidateExport(project, spec, required.GlobalPolicy);
                if (!validation.IsValid)
                {
                    Console.Error.WriteLine($"devsecrets: validation failed for {project.Name}: {validation.Error}");
                    return 1;
                }

                foreach (var secret in project.Secrets)
                {
                    if (verbose)
                    {
                        Console.WriteLine($"devsecrets: {project.Name} set {secret.Key}");
                    }

                    if (dryRun)
                    {
                        continue;
                    }

                    await dotnet.SetAsync(project.UserSecretsId, secret.Key, secret.Value, cancellationToken);
                }

                Console.WriteLine(
                    dryRun
                        ? $"devsecrets: {project.Name} dry-run complete ({project.Secrets.Count} keys)"
                        : $"devsecrets: {project.Name} imported {project.Secrets.Count} keys");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"devsecrets: import failed: {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> ValidateAsync(
        string? requiredPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var repoRoot = RepoLocator.FindRepoRoot();
            var required = SecretsRequiredConfig.Load(ResolveRequiredPath(requiredPath, repoRoot));
            var dotnet = new DotnetUserSecrets();

            var missingByProject = new List<string>();

            foreach (var project in OrderProjects(required.Projects))
            {
                if (string.IsNullOrWhiteSpace(project.UserSecretsId))
                {
                    missingByProject.Add($"{project.Name}: missing UserSecretsId");
                    continue;
                }

                var list = await dotnet.ListAsync(project.UserSecretsId, cancellationToken);
                var filter = SecretsFilter.Filter(list, project, required.GlobalPolicy);

                if (filter.MissingRequired.Count > 0)
                {
                    missingByProject.Add(
                        $"{project.Name} ({project.UserSecretsId}): {string.Join(", ", filter.MissingRequired)}");
                }
            }

            if (missingByProject.Count > 0)
            {
                Console.Error.WriteLine("devsecrets: missing required keys:");
                foreach (var line in missingByProject)
                {
                    Console.Error.WriteLine($"  - {line}");
                }

                return 1;
            }

            Console.WriteLine("devsecrets: all required keys are present.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"devsecrets: validate failed: {ex.Message}");
            return 1;
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

        return Path.Combine(repoRoot, "tools", "dev-secrets", "secrets.required.json");
    }

    private static string ResolveOutputPath(string? outPath, string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(outPath))
        {
            return Path.IsPathRooted(outPath)
                ? Path.GetFullPath(outPath)
                : Path.GetFullPath(Path.Combine(repoRoot, outPath));
        }

        var defaultPath = Path.Combine(repoRoot, "dev-secrets.export.json");
        return Path.GetFullPath(defaultPath);
    }

    internal static IReadOnlyList<ProjectSecretsConfig> OrderProjects(IEnumerable<ProjectSecretsConfig> projects)
        => projects
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
