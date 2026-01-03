namespace MGF.Data.Stores.RootIntegrity;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MGF.Contracts.Abstractions.RootIntegrity;
using MGF.Data.Data;

public sealed class RootIntegrityStore : IRootIntegrityStore
{
    private readonly AppDbContext db;

    public RootIntegrityStore(AppDbContext db)
    {
        this.db = db;
    }

    public async Task<RootIntegrityContract?> GetContractAsync(
        string providerKey,
        string rootKey,
        CancellationToken cancellationToken = default)
    {
        var row = await db.Set<Dictionary<string, object>>("storage_root_contracts")
            .Where(r =>
                EF.Property<string>(r, "provider_key") == providerKey
                && EF.Property<string>(r, "root_key") == rootKey
                && EF.Property<bool>(r, "is_active"))
            .Select(r => new
            {
                ProviderKey = EF.Property<string>(r, "provider_key"),
                RootKey = EF.Property<string>(r, "root_key"),
                ContractKey = EF.Property<string>(r, "contract_key"),
                Required = EF.Property<JsonElement>(r, "required_folders"),
                Optional = EF.Property<JsonElement>(r, "optional_folders"),
                AllowedExtras = EF.Property<JsonElement>(r, "allowed_extras"),
                AllowedRootFiles = EF.Property<JsonElement>(r, "allowed_root_files"),
                QuarantineRelpath = EF.Property<string?>(r, "quarantine_relpath"),
                MaxItems = EF.Property<int?>(r, "max_items"),
                MaxBytes = EF.Property<long?>(r, "max_bytes"),
                IsActive = EF.Property<bool>(r, "is_active")
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new RootIntegrityContract(
            ProviderKey: row.ProviderKey,
            RootKey: row.RootKey,
            ContractKey: row.ContractKey,
            RequiredFolders: ParseStringArray(row.Required),
            OptionalFolders: ParseStringArray(row.Optional),
            AllowedExtras: ParseStringArray(row.AllowedExtras),
            AllowedRootFiles: ParseStringArray(row.AllowedRootFiles),
            QuarantineRelpath: row.QuarantineRelpath,
            MaxItems: row.MaxItems,
            MaxBytes: row.MaxBytes,
            IsActive: row.IsActive
        );
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                list.Add(item.GetString()!);
            }
        }

        return list;
    }
}
