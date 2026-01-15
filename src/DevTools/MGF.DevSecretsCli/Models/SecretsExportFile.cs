namespace MGF.DevSecretsCli;

using System.Text.Json;

internal sealed class SecretsExportFile
{
    public int SchemaVersion { get; init; } = 2;
    public string ToolVersion { get; init; } = string.Empty;
    public string ExportedAtUtc { get; init; } = string.Empty;
    public string? RepoCommit { get; init; }
    public SortedDictionary<string, string> Secrets { get; init; } = new(StringComparer.Ordinal);

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

        if (export is null || export.Secrets.Count == 0)
        {
            throw new InvalidOperationException("Export file missing secrets.");
        }

        if (export.SchemaVersion != 2)
        {
            throw new InvalidOperationException($"Export file schemaVersion {export.SchemaVersion} is not supported.");
        }

        return export;
    }
}

