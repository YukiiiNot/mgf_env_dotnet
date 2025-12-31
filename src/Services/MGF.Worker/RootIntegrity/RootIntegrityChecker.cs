using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MGF.Infrastructure.Data;

namespace MGF.Worker.RootIntegrity;

public sealed class RootIntegrityChecker
{
    private static readonly string[] DefaultAllowedRootFiles = ["desktop.ini"];
    private const int DefaultMaxItems = 500;
    private const long DefaultMaxBytes = 20L * 1024 * 1024 * 1024;

    private readonly IConfiguration configuration;

    public RootIntegrityChecker(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task<RootIntegrityResult> RunAsync(
        AppDbContext db,
        RootIntegrityPayload payload,
        string jobId,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var errors = new List<string>();
        var warnings = new List<string>();
        var actions = new List<RootIntegrityAction>();
        var quarantinePlan = new List<RootIntegrityMovePlan>();
        var guardrailBlocks = new List<RootIntegrityMovePlan>();

        if (!IsReportMode(payload.Mode) && !IsRepairMode(payload.Mode))
        {
            errors.Add($"Invalid mode '{payload.Mode}'. Expected 'report' or 'repair'.");
            return BuildResult(payload, string.Empty, startedAt, errors, warnings, actions, quarantinePlan, guardrailBlocks);
        }

        var contract = await LoadContractAsync(db, payload.ProviderKey, payload.RootKey, cancellationToken);
        if (contract is null)
        {
            errors.Add($"No storage_root_contracts entry for provider_key={payload.ProviderKey} root_key={payload.RootKey}.");
            return BuildResult(payload, string.Empty, startedAt, errors, warnings, actions, quarantinePlan, guardrailBlocks);
        }

        var rootPath = ResolveRootPath(payload.ProviderKey);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            errors.Add($"Storage root not configured for provider_key={payload.ProviderKey}.");
            return BuildResult(payload, string.Empty, startedAt, errors, warnings, actions, quarantinePlan, guardrailBlocks);
        }

        rootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(rootPath))
        {
            errors.Add($"Root path does not exist: {rootPath}");
            return BuildResult(payload, rootPath, startedAt, errors, warnings, actions, quarantinePlan, guardrailBlocks);
        }

        var allowedRootFiles = payload.AllowedRootFiles ?? contract.AllowedRootFiles;
        if (allowedRootFiles.Count == 0)
        {
            allowedRootFiles = DefaultAllowedRootFiles;
        }

        var allowedExtras = payload.AllowedExtras ?? contract.AllowedExtras;

        var maxItems = payload.MaxItems ?? contract.MaxItems ?? DefaultMaxItems;
        if (maxItems <= 0)
        {
            maxItems = DefaultMaxItems;
        }

        var maxBytes = payload.MaxBytes ?? contract.MaxBytes ?? DefaultMaxBytes;
        if (maxBytes <= 0)
        {
            maxBytes = DefaultMaxBytes;
        }

        var scan = ScanRoot(rootPath, contract, allowedExtras, allowedRootFiles);

        warnings.AddRange(scan.MissingRequired.Select(name => $"missing_required:{name}"));

        var allowMeasure = IsRepairMode(payload.Mode) && !payload.DryRun;
        foreach (var entry in scan.UnknownEntries.Concat(scan.RootFiles))
        {
            var move = BuildMovePlan(entry, maxItems, maxBytes, allowMeasure);
            if (move.WillMove)
            {
                quarantinePlan.Add(move);
            }
            else
            {
                guardrailBlocks.Add(move);
            }
        }

        if (IsRepairMode(payload.Mode) && !payload.DryRun)
        {
            var quarantineRelpath = string.IsNullOrWhiteSpace(payload.QuarantineRelpath)
                ? contract.QuarantineRelpath
                : payload.QuarantineRelpath;

            if (string.IsNullOrWhiteSpace(quarantineRelpath))
            {
                errors.Add("Quarantine path is required for repair mode.");
            }
            else
            {
                var quarantineRoot = ResolveQuarantineRoot(rootPath, quarantineRelpath, out var quarantineError);
                if (!string.IsNullOrWhiteSpace(quarantineError))
                {
                    errors.Add(quarantineError);
                }
                else if (quarantineRoot is not null)
                {
                    var runQuarantine = Path.Combine(quarantineRoot, DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss"));
                    Directory.CreateDirectory(runQuarantine);
                    actions.Add(new RootIntegrityAction("create_quarantine", runQuarantine, null));

                    foreach (var missing in scan.MissingRequired)
                    {
                        try
                        {
                            var target = Path.Combine(rootPath, missing);
                            Directory.CreateDirectory(target);
                            actions.Add(new RootIntegrityAction("create_required", target, null));
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"create_required_failed:{missing}:{ex.Message}");
                        }
                    }

                    foreach (var move in quarantinePlan)
                    {
                        if (!move.WillMove)
                        {
                            continue;
                        }

                        var destination = GetUniqueDestination(runQuarantine, move.Name);
                        try
                        {
                            if (string.Equals(move.Kind, "file", StringComparison.OrdinalIgnoreCase))
                            {
                                File.Move(move.Path, destination);
                            }
                            else
                            {
                                Directory.Move(move.Path, destination);
                            }

                            actions.Add(new RootIntegrityAction("quarantine_move", destination, move.Path));
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"quarantine_move_failed:{move.Path}:{ex.Message}");
                        }
                    }
                }
            }
        }

        return BuildResult(
            payload,
            rootPath,
            startedAt,
            errors,
            warnings,
            actions,
            quarantinePlan,
            guardrailBlocks,
            scan
        );
    }

    public static bool IsRepairMode(string mode)
    {
        return string.Equals(mode, "repair", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsReportMode(string mode)
    {
        return string.Equals(mode, "report", StringComparison.OrdinalIgnoreCase);
    }

    private static RootIntegrityResult BuildResult(
        RootIntegrityPayload payload,
        string rootPath,
        DateTimeOffset startedAt,
        List<string> errors,
        List<string> warnings,
        List<RootIntegrityAction> actions,
        List<RootIntegrityMovePlan> quarantinePlan,
        List<RootIntegrityMovePlan> guardrailBlocks,
        RootIntegrityScan? scan = null)
    {
        var finishedAt = DateTimeOffset.UtcNow;

        return new RootIntegrityResult(
            ProviderKey: payload.ProviderKey,
            RootKey: payload.RootKey,
            RootPath: rootPath,
            Mode: payload.Mode,
            DryRun: payload.DryRun,
            StartedAt: startedAt,
            FinishedAt: finishedAt,
            MissingRequired: scan?.MissingRequired ?? Array.Empty<string>(),
            MissingOptional: scan?.MissingOptional ?? Array.Empty<string>(),
            UnknownEntries: scan?.UnknownEntries ?? Array.Empty<RootIntegrityEntry>(),
            RootFiles: scan?.RootFiles ?? Array.Empty<RootIntegrityEntry>(),
            QuarantinePlan: quarantinePlan,
            GuardrailBlocks: guardrailBlocks,
            Actions: actions,
            Warnings: warnings,
            Errors: errors
        );
    }

    internal static RootIntegrityScan ScanRoot(
        string rootPath,
        RootIntegrityContract contract,
        IReadOnlyList<string> allowedExtras,
        IReadOnlyList<string> allowedRootFiles)
    {
        var required = new HashSet<string>(contract.RequiredFolders, StringComparer.OrdinalIgnoreCase);
        var optional = new HashSet<string>(contract.OptionalFolders, StringComparer.OrdinalIgnoreCase);
        var allowedRootFilesSet = new HashSet<string>(allowedRootFiles, StringComparer.OrdinalIgnoreCase);

        var entries = new List<FileSystemInfo>();
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        var rootInfo = new DirectoryInfo(rootPath);
        foreach (var entry in rootInfo.EnumerateFileSystemInfos("*", options))
        {
            entries.Add(entry);
        }

        var names = new HashSet<string>(entries.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
        var missingRequired = required.Where(name => !names.Contains(name)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        var missingOptional = optional.Where(name => !names.Contains(name)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        var unknownEntries = new List<RootIntegrityEntry>();
        var rootFiles = new List<RootIntegrityEntry>();
        foreach (var entry in entries)
        {
            var isDir = entry is DirectoryInfo;
            var isReparse = (entry.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;

            if (isDir)
            {
                if (required.Contains(entry.Name) || optional.Contains(entry.Name) || MatchesAllowedExtras(entry.Name, allowedExtras))
                {
                    continue;
                }

                var unknown = new RootIntegrityEntry(
                    Name: entry.Name,
                    Path: entry.FullName,
                    Kind: "folder",
                    IsReparsePoint: isReparse,
                    SizeBytes: null,
                    ItemCount: null,
                    Note: isReparse ? "reparse_point" : null
                );
                unknownEntries.Add(unknown);
            }
            else
            {
                if (allowedRootFilesSet.Contains(entry.Name))
                {
                    continue;
                }

                var fileInfo = (FileInfo)entry;
                var rootFile = new RootIntegrityEntry(
                    Name: entry.Name,
                    Path: entry.FullName,
                    Kind: "file",
                    IsReparsePoint: isReparse,
                    SizeBytes: isReparse ? null : fileInfo.Length,
                    ItemCount: 1,
                    Note: isReparse ? "reparse_point" : null
                );
                rootFiles.Add(rootFile);
            }
        }

        return new RootIntegrityScan(missingRequired, missingOptional, unknownEntries, rootFiles);
    }

    internal static RootIntegrityMovePlan BuildMovePlan(
        RootIntegrityEntry entry,
        int maxItems,
        long maxBytes,
        bool allowMeasure)
    {
        if (entry.IsReparsePoint)
        {
            return new RootIntegrityMovePlan(entry.Name, entry.Path, entry.Kind, null, null, "size_unknown");
        }

        if (string.Equals(entry.Kind, "file", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(entry.Path))
            {
                return new RootIntegrityMovePlan(entry.Name, entry.Path, entry.Kind, entry.SizeBytes, entry.ItemCount, "missing");
            }

            var size = entry.SizeBytes ?? new FileInfo(entry.Path).Length;
            var blocked = size > maxBytes ? "too_large_to_quarantine" : null;
            return new RootIntegrityMovePlan(entry.Name, entry.Path, entry.Kind, size, 1, blocked);
        }

        if (!allowMeasure)
        {
            return new RootIntegrityMovePlan(entry.Name, entry.Path, entry.Kind, null, null, "size_unknown");
        }

        var measurement = MeasureDirectory(entry.Path, maxItems, maxBytes);
        if (measurement.Error is not null)
        {
            return new RootIntegrityMovePlan(entry.Name, entry.Path, entry.Kind, null, null, measurement.Error);
        }

        return new RootIntegrityMovePlan(entry.Name, entry.Path, entry.Kind, measurement.SizeBytes, measurement.ItemCount, null);
    }

    internal static bool MatchesAllowedExtras(string name, IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (!pattern.Contains('*', StringComparison.Ordinal))
            {
                if (string.Equals(pattern, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
            if (Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string? ResolveRootPath(string providerKey)
    {
        return providerKey.ToLowerInvariant() switch
        {
            "dropbox" => configuration["Storage:DropboxRoot"],
            "lucidlink" => configuration["Storage:LucidLinkRoot"],
            "nas" => configuration["Storage:NasRoot"],
            _ => null
        };
    }

    private static string? ResolveQuarantineRoot(string rootPath, string quarantineRelpath, out string? error)
    {
        error = null;
        var combined = Path.Combine(rootPath, quarantineRelpath);
        var fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullQuarantine = Path.GetFullPath(combined).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!fullQuarantine.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Quarantine path must be inside root. root={rootPath} quarantine={quarantineRelpath}";
            return null;
        }

        Directory.CreateDirectory(fullQuarantine);
        return fullQuarantine;
    }

    private static string GetUniqueDestination(string quarantineRoot, string name)
    {
        var destination = Path.Combine(quarantineRoot, name);
        if (!File.Exists(destination) && !Directory.Exists(destination))
        {
            return destination;
        }

        var suffix = 1;
        while (true)
        {
            var candidate = Path.Combine(quarantineRoot, $"{name}_{suffix}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static async Task<RootIntegrityContract?> LoadContractAsync(
        AppDbContext db,
        string providerKey,
        string rootKey,
        CancellationToken cancellationToken)
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

    public static string BuildJobPayloadJson(RootIntegrityPayload payload, RootIntegrityResult result)
    {
        var root = JsonSerializer.SerializeToNode(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) as JsonObject ?? new JsonObject();

        root["result"] = JsonSerializer.SerializeToNode(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return root.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    internal sealed record RootIntegrityScan(
        IReadOnlyList<string> MissingRequired,
        IReadOnlyList<string> MissingOptional,
        IReadOnlyList<RootIntegrityEntry> UnknownEntries,
        IReadOnlyList<RootIntegrityEntry> RootFiles
    );

    internal sealed record DirectoryMeasurement(long SizeBytes, int ItemCount, string? Error);

    internal static DirectoryMeasurement MeasureDirectory(string path, int maxItems, long maxBytes)
    {
        try
        {
            var root = new DirectoryInfo(path);
            if (!root.Exists)
            {
                return new DirectoryMeasurement(0, 0, "missing");
            }

            if ((root.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                return new DirectoryMeasurement(0, 0, "size_unknown");
            }

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false
            };

            long totalBytes = 0;
            var totalItems = 0;

            foreach (var entry in root.EnumerateFileSystemInfos("*", options))
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    continue;
                }

                totalItems++;
                if (totalItems > maxItems)
                {
                    return new DirectoryMeasurement(totalBytes, totalItems, "too_large_to_quarantine");
                }

                if (entry is FileInfo file)
                {
                    totalBytes += file.Length;
                    if (totalBytes > maxBytes)
                    {
                        return new DirectoryMeasurement(totalBytes, totalItems, "too_large_to_quarantine");
                    }
                }
            }

            return new DirectoryMeasurement(totalBytes, totalItems, null);
        }
        catch (Exception)
        {
            return new DirectoryMeasurement(0, 0, "size_unknown");
        }
    }
}
