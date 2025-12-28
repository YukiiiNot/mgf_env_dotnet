using System.Text.Json;

namespace MGF.Worker.RootIntegrity;

public sealed record RootIntegrityPayload(
    string ProviderKey,
    string RootKey,
    string Mode,
    bool DryRun,
    string? QuarantineRelpath,
    int? MaxItems,
    long? MaxBytes,
    IReadOnlyList<string>? AllowedExtras,
    IReadOnlyList<string>? AllowedRootFiles
)
{
    public static RootIntegrityPayload Parse(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;

        var providerKey = GetString(root, "providerKey");
        var rootKey = GetString(root, "rootKey");
        var mode = GetString(root, "mode");

        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new InvalidOperationException("providerKey is required.");
        }

        if (string.IsNullOrWhiteSpace(rootKey))
        {
            rootKey = "root";
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            mode = "report";
        }

        var dryRun = GetBool(root, "dryRun") ?? true;
        var quarantineRelpath = GetString(root, "quarantineRelpath");
        var maxItems = GetInt(root, "maxItems");
        var maxBytes = GetLong(root, "maxBytes");

        return new RootIntegrityPayload(
            ProviderKey: providerKey,
            RootKey: rootKey,
            Mode: mode,
            DryRun: dryRun,
            QuarantineRelpath: quarantineRelpath,
            MaxItems: maxItems,
            MaxBytes: maxBytes,
            AllowedExtras: GetStringArray(root, "allowedExtras"),
            AllowedRootFiles: GetStringArray(root, "allowedRootFiles")
        );
    }

    private static string? GetString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? GetBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }

    private static int? GetInt(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;
    }

    private static long? GetLong(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed)
            ? parsed
            : null;
    }

    private static IReadOnlyList<string>? GetStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                result.Add(item.GetString()!);
            }
        }

        return result.Count == 0 ? null : result;
    }
}

public sealed record RootIntegrityContract(
    string ProviderKey,
    string RootKey,
    string ContractKey,
    IReadOnlyList<string> RequiredFolders,
    IReadOnlyList<string> OptionalFolders,
    IReadOnlyList<string> AllowedExtras,
    IReadOnlyList<string> AllowedRootFiles,
    string? QuarantineRelpath,
    int? MaxItems,
    long? MaxBytes,
    bool IsActive
);

public sealed record RootIntegrityEntry(
    string Name,
    string Path,
    string Kind,
    bool IsReparsePoint,
    long? SizeBytes,
    int? ItemCount,
    string? Note
);

public sealed record RootIntegrityMovePlan(
    string Name,
    string Path,
    string Kind,
    long? SizeBytes,
    int? ItemCount,
    string? BlockedReason
)
{
    public bool WillMove => string.IsNullOrWhiteSpace(BlockedReason);
}

public sealed record RootIntegrityAction(
    string Action,
    string Path,
    string? Note
);

public sealed record RootIntegrityResult(
    string ProviderKey,
    string RootKey,
    string RootPath,
    string Mode,
    bool DryRun,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<string> MissingRequired,
    IReadOnlyList<string> MissingOptional,
    IReadOnlyList<RootIntegrityEntry> UnknownEntries,
    IReadOnlyList<RootIntegrityEntry> RootFiles,
    IReadOnlyList<RootIntegrityMovePlan> QuarantinePlan,
    IReadOnlyList<RootIntegrityMovePlan> GuardrailBlocks,
    IReadOnlyList<RootIntegrityAction> Actions,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors
)
{
    public bool HasErrors => Errors.Count > 0;
}
