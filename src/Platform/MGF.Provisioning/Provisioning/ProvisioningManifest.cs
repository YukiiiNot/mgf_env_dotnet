using System.Text.Json;
using MGF.Contracts.Abstractions;

namespace MGF.Provisioning;

public sealed class ProvisioningManifest
{
    public string TemplateKey { get; set; } = string.Empty;
    public string TemplateHash { get; set; } = string.Empty;
    public string RunMode { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public Dictionary<string, object?> Tokens { get; set; } = new();
    public string TargetRoot { get; set; } = string.Empty;
    public List<ManifestItem> ExpectedItems { get; set; } = new();
    public List<ManifestItem> CreatedItems { get; set; } = new();
    public List<string> MissingRequired { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public sealed class ManifestItem
{
    public string Path { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public bool Optional { get; set; }
}

public static class ProvisioningManifestWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task WriteAsync(
        IFileStore fileStore,
        ProvisioningManifest manifest,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        var content = JsonSerializer.Serialize(manifest, SerializerOptions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        var directory = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            await fileStore.CreateDirectoryAsync(directory, cancellationToken);
        }

        await fileStore.WriteAllBytesAsync(manifestPath, bytes, cancellationToken);
    }
}


