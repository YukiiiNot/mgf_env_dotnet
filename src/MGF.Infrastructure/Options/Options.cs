namespace MGF.Infrastructure.Options;

public sealed class StorageRootsOptions
{
    public string DropboxRoot { get; init; } = string.Empty;
    public string NasRoot { get; init; } = string.Empty;
    public string RuntimeRoot { get; init; } = string.Empty;
}

public sealed class DatabaseOptions
{
    public string ConnectionString { get; init; } = string.Empty;
}

public sealed class FeatureFlagsOptions
{
    public Dictionary<string, bool> Flags { get; init; } = new();
}
