using System.Text.Json;

namespace MGF.ProjectBootstrapCli;

public sealed class DevTestRootCleaner
{
    private static readonly string[] DefaultAllowedRootFiles = ["desktop.ini"];

    public static DevTestRootPlan Plan(
        string rootPath,
        DevTestRootContract contract,
        DevTestRootOptions options)
    {
        var required = new HashSet<string>(contract.RequiredFolders, StringComparer.OrdinalIgnoreCase);
        var optional = new HashSet<string>(contract.OptionalFolders, StringComparer.OrdinalIgnoreCase);
        var allowedExtras = contract.AllowedExtras ?? Array.Empty<string>();
        var allowedRootFiles = contract.AllowedRootFiles ?? DefaultAllowedRootFiles;

        var entries = new List<FileSystemInfo>();
        var optionsEnum = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        var rootInfo = new DirectoryInfo(rootPath);
        foreach (var entry in rootInfo.EnumerateFileSystemInfos("*", optionsEnum))
        {
            entries.Add(entry);
        }

        var names = new HashSet<string>(entries.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
        var missingRequired = required.Where(name => !names.Contains(name)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        var missingOptional = optional.Where(name => !names.Contains(name)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        var unknownEntries = new List<RootEntry>();
        var rootFiles = new List<RootEntry>();
        var legacyManifestFiles = new List<LegacyManifestFile>();

        foreach (var entry in entries)
        {
            var isDir = entry is DirectoryInfo;
            var isReparse = (entry.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;

            if (isDir)
            {
                if (required.Contains(entry.Name) || optional.Contains(entry.Name) || MatchesAllowedExtras(entry.Name, allowedExtras))
                {
                    var legacyInFolder = FindLegacyManifestInFolder(entry.FullName, entry.Name);
                    if (legacyInFolder is not null)
                    {
                        legacyManifestFiles.Add(legacyInFolder);
                    }

                    continue;
                }

                var unknown = new RootEntry(
                    Name: entry.Name,
                    Path: entry.FullName,
                    Kind: "folder",
                    IsReparsePoint: isReparse,
                    SizeBytes: null,
                    ItemCount: null
                );
                unknownEntries.Add(unknown);
            }
            else
            {
                if (string.Equals(entry.Name, "folder_manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    legacyManifestFiles.Add(new LegacyManifestFile(entry.FullName, "root"));
                    continue;
                }

                if (allowedRootFiles.Contains(entry.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileInfo = (FileInfo)entry;
                rootFiles.Add(new RootEntry(
                    Name: entry.Name,
                    Path: entry.FullName,
                    Kind: "file",
                    IsReparsePoint: isReparse,
                    SizeBytes: isReparse ? null : fileInfo.Length,
                    ItemCount: 1
                ));
            }
        }

        var unknownPlans = new List<MovePlan>();
        var guardrailBlocks = new List<MovePlan>();
        foreach (var entry in unknownEntries.Concat(rootFiles))
        {
            var plan = BuildMovePlan(entry, options.MaxItems, options.MaxBytes, options.ForceUnknownSize, allowMeasure: options.AllowMeasure);
            if (plan.WillMove)
            {
                unknownPlans.Add(plan);
            }
            else
            {
                guardrailBlocks.Add(plan);
            }
        }

        var legacyPlans = BuildLegacyPlans(rootPath, legacyManifestFiles, options.TimestampUtc);

        return new DevTestRootPlan(
            RootPath: rootPath,
            MissingRequired: missingRequired,
            MissingOptional: missingOptional,
            UnknownEntries: unknownEntries,
            RootFiles: rootFiles,
            LegacyManifestFiles: legacyManifestFiles,
            UnknownMovePlans: unknownPlans,
            GuardrailBlocks: guardrailBlocks,
            LegacyMovePlans: legacyPlans
        );
    }

    public static DevTestRootApplyResult Apply(
        DevTestRootPlan plan,
        DevTestRootContract contract,
        DevTestRootOptions options)
    {
        var actions = new List<string>();
        var errors = new List<string>();

        if (options.DryRun)
        {
            return new DevTestRootApplyResult(actions, errors);
        }

        foreach (var missing in plan.MissingRequired)
        {
            try
            {
                var target = Path.Combine(plan.RootPath, missing);
                Directory.CreateDirectory(target);
                actions.Add($"create_required:{target}");
            }
            catch (Exception ex)
            {
                errors.Add($"create_required_failed:{missing}:{ex.Message}");
            }
        }

        try
        {
            var manifestDir = Path.Combine(plan.RootPath, "00_Admin", ".mgf", "manifest");
            Directory.CreateDirectory(manifestDir);
            actions.Add($"ensure_manifest_dir:{manifestDir}");
        }
        catch (Exception ex)
        {
            errors.Add($"ensure_manifest_dir_failed:{ex.Message}");
        }

        var quarantineRelpath = string.IsNullOrWhiteSpace(contract.QuarantineRelpath)
            ? "99_Dump/_quarantine"
            : contract.QuarantineRelpath;
        var quarantineRoot = Path.Combine(plan.RootPath, quarantineRelpath);
        Directory.CreateDirectory(quarantineRoot);
        var quarantineRun = Path.Combine(quarantineRoot, options.TimestampUtc.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(quarantineRun);

        foreach (var move in plan.UnknownMovePlans)
        {
            if (!move.WillMove)
            {
                continue;
            }

            var destination = GetUniqueDestination(quarantineRun, move.Name);
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

                actions.Add($"quarantine_move:{move.Path}=>{destination}");
            }
            catch (Exception ex)
            {
                errors.Add($"quarantine_move_failed:{move.Path}:{ex.Message}");
            }
        }

        var legacyRoot = Path.Combine(plan.RootPath, "00_Admin", ".mgf", "manifest", "_legacy", options.TimestampUtc.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(legacyRoot);

        foreach (var legacy in plan.LegacyMovePlans)
        {
            try
            {
                if (!File.Exists(legacy.SourcePath))
                {
                    continue;
                }

                var destination = GetUniqueDestination(legacyRoot, legacy.DestinationFileName);
                File.Move(legacy.SourcePath, destination);
                actions.Add($"legacy_manifest_move:{legacy.SourcePath}=>{destination}");
            }
            catch (Exception ex)
            {
                errors.Add($"legacy_manifest_move_failed:{legacy.SourcePath}:{ex.Message}");
            }
        }

        return new DevTestRootApplyResult(actions, errors);
    }

    public static bool MatchesAllowedExtras(string name, IReadOnlyList<string> patterns)
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

            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
            if (System.Text.RegularExpressions.Regex.IsMatch(name, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static MovePlan BuildMovePlan(
        RootEntry entry,
        int maxItems,
        long maxBytes,
        bool forceUnknownSize,
        bool allowMeasure)
    {
        if (entry.IsReparsePoint)
        {
            return new MovePlan(entry.Name, entry.Path, entry.Kind, entry.SizeBytes, entry.ItemCount, forceUnknownSize, "unknown_size");
        }

        if (string.Equals(entry.Kind, "file", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(entry.Path))
            {
                return new MovePlan(entry.Name, entry.Path, entry.Kind, entry.SizeBytes, entry.ItemCount, false, "missing");
            }

            var size = entry.SizeBytes ?? new FileInfo(entry.Path).Length;
            var blocked = size > maxBytes ? "too_large_to_quarantine" : null;
            var willMove = blocked is null;
            return new MovePlan(entry.Name, entry.Path, entry.Kind, size, 1, willMove, blocked);
        }

        if (!allowMeasure)
        {
            return new MovePlan(entry.Name, entry.Path, entry.Kind, entry.SizeBytes, entry.ItemCount, forceUnknownSize, "unknown_size");
        }

        var measurement = MeasureDirectory(entry.Path, maxItems, maxBytes);
        if (measurement.BlockedReason is not null)
        {
            var willMove = forceUnknownSize && measurement.BlockedReason == "unknown_size";
            return new MovePlan(entry.Name, entry.Path, entry.Kind, measurement.SizeBytes, measurement.ItemCount, willMove, measurement.BlockedReason);
        }

        return new MovePlan(entry.Name, entry.Path, entry.Kind, measurement.SizeBytes, measurement.ItemCount, true, null);
    }

    private static List<LegacyMovePlan> BuildLegacyPlans(
        string rootPath,
        IReadOnlyList<LegacyManifestFile> legacyFiles,
        DateTimeOffset timestampUtc)
    {
        var plans = new List<LegacyMovePlan>();
        foreach (var legacy in legacyFiles)
        {
            var name = legacy.Scope == "root"
                ? "root__folder_manifest.json"
                : $"{legacy.Scope}__folder_manifest.json";

            plans.Add(new LegacyMovePlan(legacy.SourcePath, name));
        }

        return plans;
    }

    private static LegacyManifestFile? FindLegacyManifestInFolder(string folderPath, string scopeName)
    {
        var candidate = Path.Combine(folderPath, "folder_manifest.json");
        return File.Exists(candidate) ? new LegacyManifestFile(candidate, scopeName) : null;
    }

    private static DirectoryMeasurement MeasureDirectory(string path, int maxItems, long maxBytes)
    {
        try
        {
            var root = new DirectoryInfo(path);
            if (!root.Exists)
            {
                return new DirectoryMeasurement(null, null, "missing");
            }

            if ((root.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                return new DirectoryMeasurement(null, null, "unknown_size");
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
            return new DirectoryMeasurement(null, null, "unknown_size");
        }
    }

    private static string GetUniqueDestination(string root, string fileName)
    {
        var destination = Path.Combine(root, fileName);
        if (!File.Exists(destination))
        {
            return destination;
        }

        var suffix = 1;
        while (true)
        {
            var candidate = Path.Combine(root, $"{Path.GetFileNameWithoutExtension(fileName)}_{suffix}{Path.GetExtension(fileName)}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }
}

public sealed record DevTestRootOptions(
    bool DryRun,
    int MaxItems,
    long MaxBytes,
    bool ForceUnknownSize,
    bool AllowMeasure,
    DateTimeOffset TimestampUtc
);

public sealed record DevTestRootContract(
    IReadOnlyList<string> RequiredFolders,
    IReadOnlyList<string> OptionalFolders,
    IReadOnlyList<string> AllowedExtras,
    IReadOnlyList<string> AllowedRootFiles,
    string? QuarantineRelpath
);

public sealed record RootEntry(
    string Name,
    string Path,
    string Kind,
    bool IsReparsePoint,
    long? SizeBytes,
    int? ItemCount
);

public sealed record MovePlan(
    string Name,
    string Path,
    string Kind,
    long? SizeBytes,
    int? ItemCount,
    bool WillMove,
    string? BlockedReason
);

public sealed record LegacyManifestFile(string SourcePath, string Scope);

public sealed record LegacyMovePlan(string SourcePath, string DestinationFileName);

public sealed record DirectoryMeasurement(long? SizeBytes, int? ItemCount, string? BlockedReason);

public sealed record DevTestRootPlan(
    string RootPath,
    IReadOnlyList<string> MissingRequired,
    IReadOnlyList<string> MissingOptional,
    IReadOnlyList<RootEntry> UnknownEntries,
    IReadOnlyList<RootEntry> RootFiles,
    IReadOnlyList<LegacyManifestFile> LegacyManifestFiles,
    IReadOnlyList<MovePlan> UnknownMovePlans,
    IReadOnlyList<MovePlan> GuardrailBlocks,
    IReadOnlyList<LegacyMovePlan> LegacyMovePlans
);

public sealed record DevTestRootApplyResult(
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Errors
);
