namespace MGF.Infrastructure.Options;

public sealed class StorageRootsOptions
{
    public string DropboxRoot { get; init; } = string.Empty;
    public string NasRoot { get; init; } = string.Empty;
    public string RuntimeRoot { get; init; } = string.Empty;
}

public sealed class DatabaseOptions
{
    public DatabaseEnvironmentOptions Dev { get; init; } = new();
    public DatabaseEnvironmentOptions Staging { get; init; } = new();
    public DatabaseEnvironmentOptions Prod { get; init; } = new();

    // Legacy single connection string (fallback when env-specific key is missing).
    public string ConnectionString { get; init; } = string.Empty;
}

public sealed class DatabaseEnvironmentOptions
{
    public string ConnectionString { get; init; } = string.Empty;
}

public sealed class FeatureFlagsOptions
{
    public Dictionary<string, bool> Flags { get; init; } = new();
}
