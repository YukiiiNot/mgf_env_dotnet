namespace MGF.Tools.DevSecrets;

using System.Text.Json;

internal sealed class SecretsExportFile
{
    public int SchemaVersion { get; init; } = 1;
    public string ToolVersion { get; init; } = string.Empty;
    public string ExportedAtUtc { get; init; } = string.Empty;
    public string? RepoCommit { get; init; }
    public List<ProjectSecretsExport> Projects { get; init; } = new();

    public static async Task<SecretsExportFile> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Export file not found at {path}");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var export = JsonSerializer.Deserialize<SecretsExportFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (export is null || export.Projects.Count == 0)
        {
            throw new InvalidOperationException("Export file missing projects.");
        }

        if (export.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Export file schemaVersion {export.SchemaVersion} is not supported.");
        }

        return export;
    }
}

internal sealed class ProjectSecretsExport
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string UserSecretsId { get; init; } = string.Empty;
    public SortedDictionary<string, string> Secrets { get; init; } = new(StringComparer.Ordinal);
}
