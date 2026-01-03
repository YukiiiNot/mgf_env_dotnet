namespace MGF.Data.Stores.Operations;

using Microsoft.Extensions.Configuration;
using Npgsql;
using MGF.Contracts.Abstractions.Operations.StorageRoots;
using MGF.Data.Configuration;

public sealed class StorageRootContractStore : IStorageRootContractStore
{
    private readonly string connectionString;

    public StorageRootContractStore(IConfiguration configuration)
    {
        connectionString = DatabaseConnection.ResolveConnectionString(configuration);
    }

    public async Task<StorageRootContract?> GetActiveContractAsync(
        string providerKey,
        string rootKey,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT required_folders,
                   optional_folders,
                   allowed_extras,
                   allowed_root_files,
                   quarantine_relpath
            FROM storage_root_contracts
            WHERE provider_key = @provider
              AND root_key = @root_key
              AND is_active
            LIMIT 1;
            """,
            conn
        );
        cmd.Parameters.AddWithValue("provider", providerKey);
        cmd.Parameters.AddWithValue("root_key", rootKey);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var required = ParseJsonArray(reader.GetString(0));
        var optional = ParseJsonArray(reader.GetString(1));
        var allowedExtras = ParseJsonArray(reader.GetString(2));
        var allowedRootFiles = ParseJsonArray(reader.GetString(3));
        var quarantineRelpath = reader.IsDBNull(4) ? null : reader.GetString(4);

        return new StorageRootContract(required, optional, allowedExtras, allowedRootFiles, quarantineRelpath);
    }

    private static IReadOnlyList<string> ParseJsonArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                list.Add(item.GetString()!);
            }
        }

        return list;
    }
}
