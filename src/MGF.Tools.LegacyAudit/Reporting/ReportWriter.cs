
using System.Text;
using System.Text.Json;
using MGF.Tools.LegacyAudit.Models;
using MGF.Tools.LegacyAudit.Scanning;

namespace MGF.Tools.LegacyAudit.Reporting;

internal static class ReportWriter
{
    public static void WriteAll(ScanReport report, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var jsonPath = Path.Combine(outputDir, "scan_report.json");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json, new UTF8Encoding(false));

        WriteSummary(report, Path.Combine(outputDir, "scan_summary.txt"));
        WriteCsvs(report, outputDir);
    }

    public static void WriteCsvs(ScanReport report, string outputDir)
    {
        var rootShare = PathHelpers.GetRootShare(report.ScanInfo.RootPath);
        var duplicateFingerprints = report.DuplicateCandidates
            .Where(item => item.DupType == "folder")
            .Select(item => item.GroupKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var triageRows = report.Classifications
            .Select(item => new
            {
                Item = item,
                IsDuplicate = !string.IsNullOrWhiteSpace(item.FolderFingerprint) && duplicateFingerprints.Contains(item.FolderFingerprint),
                ImmediateCacheCount = item.ImmediateAutoSaveFolderCount + item.ImmediateAudioPreviewFolderCount + item.ImmediateVideoPreviewFolderCount
            })
            .Select(item => new
            {
                item.Item,
                item.IsDuplicate,
                item.ImmediateCacheCount,
                Priority = GetTriagePriority(item.Item, item.IsDuplicate, item.ImmediateCacheCount)
            })
            .OrderBy(item => item.Priority)
            .ThenByDescending(item => item.Item.SizeBytes)
            .Select(item => new List<string?>
            {
                rootShare,
                item.Item.RelativePath,
                item.Item.Path,
                item.Item.Classification,
                item.Priority.ToString(),
                item.Item.Confidence.ToString("0.###"),
                FormatReasons(item.Item.Reasons),
                item.Item.SizeBytes.ToString(),
                ToGiB(item.Item.SizeBytes),
                ToMiB(item.Item.SizeBytes),
                item.Item.FileCount.ToString(),
                item.Item.DirCount.ToString(),
                item.Item.ImmediatePrprojCount.ToString(),
                item.ImmediateCacheCount.ToString(),
                item.IsDuplicate ? "yes" : "no",
                item.Item.Marker?.Kind ?? string.Empty,
                item.Item.Marker?.ConfirmedBy ?? string.Empty,
                item.Item.Marker?.ConfirmedAt ?? string.Empty
            })
            .ToList();

        CsvWriter.Write(Path.Combine(outputDir, "triage.csv"),
            new[]
            {
                "root_share", "relative_path", "path", "classification", "triage_priority", "confidence", "reasons",
                "size_bytes", "size_gib", "size_mib", "file_count", "dir_count",
                "immediate_prproj_count", "immediate_cache_folder_count", "duplicate_fingerprint",
                "marker_kind", "marker_confirmed_by", "marker_confirmed_at"
            },
            triageRows);

        CsvWriter.Write(Path.Combine(outputDir, "projects_candidates.csv"),
            new[]
            {
                "root_share", "relative_path", "path", "confidence", "reasons",
                "project_root_confidence", "project_root_reasons",
                "size_bytes", "size_gib", "size_mib", "file_count", "dir_count",
                "prproj_count", "aep_count", "video_count", "audio_count", "image_count",
                "immediate_prproj_count", "immediate_cache_folder_count"
            },
            report.Classifications
                .Where(item => item.Classification == "project_root")
                .OrderByDescending(item => item.SizeBytes)
                .Select(item => new List<string?>
                {
                    rootShare,
                    item.RelativePath,
                    item.Path,
                    item.Confidence.ToString("0.###"),
                    FormatReasons(item.Reasons),
                    item.ProjectRootConfidence.ToString("0.###"),
                    FormatReasons(item.ProjectRootReasons),
                    item.SizeBytes.ToString(),
                    ToGiB(item.SizeBytes),
                    ToMiB(item.SizeBytes),
                    item.FileCount.ToString(),
                    item.DirCount.ToString(),
                    item.PrprojCount.ToString(),
                    item.AepCount.ToString(),
                    item.VideoCount.ToString(),
                    item.AudioCount.ToString(),
                    item.ImageCount.ToString(),
                    item.ImmediatePrprojCount.ToString(),
                    (item.ImmediateAutoSaveFolderCount + item.ImmediateAudioPreviewFolderCount + item.ImmediateVideoPreviewFolderCount).ToString()
                }));

        CsvWriter.Write(Path.Combine(outputDir, "project_containers.csv"),
            new[]
            {
                "root_share", "relative_path", "path", "classification", "confidence", "reasons",
                "project_container_confidence", "project_container_reasons",
                "size_bytes", "size_gib", "size_mib", "file_count", "dir_count"
            },
            report.Classifications
                .Where(item => item.Classification == "project_container" || item.Classification == "container_confirmed")
                .OrderByDescending(item => item.SizeBytes)
                .Select(item => new List<string?>
                {
                    rootShare,
                    item.RelativePath,
                    item.Path,
                    item.Classification,
                    item.Confidence.ToString("0.###"),
                    FormatReasons(item.Reasons),
                    item.ProjectContainerConfidence.ToString("0.###"),
                    FormatReasons(item.ProjectContainerReasons),
                    item.SizeBytes.ToString(),
                    ToGiB(item.SizeBytes),
                    ToMiB(item.SizeBytes),
                    item.FileCount.ToString(),
                    item.DirCount.ToString()
                }));

        CsvWriter.Write(Path.Combine(outputDir, "projects_confirmed.csv"),
            new[]
            {
                "root_share", "relative_path", "path", "classification", "confidence", "reasons",
                "size_bytes", "size_gib", "size_mib", "file_count", "dir_count",
                "marker_kind", "marker_confirmed_by", "marker_confirmed_at", "marker_legacy_client",
                "marker_legacy_project_name", "marker_status", "marker_notes", "marker_tags"
            },
            report.Classifications
                .Where(item => item.Classification == "project_confirmed" || item.Classification == "container_confirmed")
                .OrderByDescending(item => item.SizeBytes)
                .Select(item => new List<string?>
                {
                    rootShare,
                    item.RelativePath,
                    item.Path,
                    item.Classification,
                    item.Confidence.ToString("0.###"),
                    FormatReasons(item.Reasons),
                    item.SizeBytes.ToString(),
                    ToGiB(item.SizeBytes),
                    ToMiB(item.SizeBytes),
                    item.FileCount.ToString(),
                    item.DirCount.ToString(),
                    item.Marker?.Kind ?? string.Empty,
                    item.Marker?.ConfirmedBy ?? string.Empty,
                    item.Marker?.ConfirmedAt ?? string.Empty,
                    item.Marker?.LegacyClient ?? string.Empty,
                    item.Marker?.LegacyProjectName ?? string.Empty,
                    item.Marker?.Status ?? string.Empty,
                    item.Marker?.Notes ?? string.Empty,
                    item.Marker?.Tags is null ? string.Empty : string.Join(";", item.Marker.Tags)
                }));

        CsvWriter.Write(Path.Combine(outputDir, "camera_dumps.csv"),
            new[]
            {
                "root_share", "relative_path", "path", "confidence", "reasons",
                "size_bytes", "size_gib", "size_mib", "file_count", "dir_count",
                "video_count", "audio_count", "image_count", "raw_video_count",
                "likely_project_container_path", "likely_project_root_path"
            },
            report.Classifications
                .Where(item => item.Classification == "camera_dump_subtree")
                .OrderByDescending(item => item.SizeBytes)
                .Select(item => new List<string?>
                {
                    rootShare,
                    item.RelativePath,
                    item.Path,
                    item.Confidence.ToString("0.###"),
                    FormatReasons(item.Reasons),
                    item.SizeBytes.ToString(),
                    ToGiB(item.SizeBytes),
                    ToMiB(item.SizeBytes),
                    item.FileCount.ToString(),
                    item.DirCount.ToString(),
                    item.VideoCount.ToString(),
                    item.AudioCount.ToString(),
                    item.ImageCount.ToString(),
                    item.ImmediateRawVideoCount.ToString(),
                    item.LikelyProjectContainerPath ?? string.Empty,
                    item.LikelyProjectRootPath ?? string.Empty
                }));

        CsvWriter.Write(Path.Combine(outputDir, "templates.csv"),
            new[]
            {
                "root_share", "relative_path", "path", "confidence", "reasons",
                "template_pack_confidence", "template_pack_reasons",
                "size_bytes", "size_gib", "size_mib", "file_count", "dir_count", "prproj_count", "aep_count"
            },
            report.Classifications
                .Where(item => item.Classification == "template_pack")
                .OrderByDescending(item => item.SizeBytes)
                .Select(item => new List<string?>
                {
                    rootShare,
                    item.RelativePath,
                    item.Path,
                    item.Confidence.ToString("0.###"),
                    FormatReasons(item.Reasons),
                    item.TemplatePackConfidence.ToString("0.###"),
                    FormatReasons(item.TemplatePackReasons),
                    item.SizeBytes.ToString(),
                    ToGiB(item.SizeBytes),
                    ToMiB(item.SizeBytes),
                    item.FileCount.ToString(),
                    item.DirCount.ToString(),
                    item.PrprojCount.ToString(),
                    item.AepCount.ToString()
                }));

        CsvWriter.Write(Path.Combine(outputDir, "cache_folders.csv"),
            new[]
            {
                "root_share", "relative_path", "path", "cache_type", "parent_path", "parent_relative_path", "parent_cache_count"
            },
            BuildCacheRows(report, rootShare));

        CsvWriter.Write(Path.Combine(outputDir, "empty_folders.csv"),
            new[]
            {
                "root_share", "relative_path", "path", "confidence", "reasons", "size_bytes", "size_gib", "size_mib", "file_count", "dir_count"
            },
            report.Classifications
                .Where(item => item.Classification == "empty_folder")
                .OrderBy(item => item.Path)
                .Select(item => new List<string?>
                {
                    rootShare,
                    item.RelativePath,
                    item.Path,
                    item.Confidence.ToString("0.###"),
                    FormatReasons(item.Reasons),
                    item.SizeBytes.ToString(),
                    ToGiB(item.SizeBytes),
                    ToMiB(item.SizeBytes),
                    item.FileCount.ToString(),
                    item.DirCount.ToString()
                }));

        CsvWriter.Write(Path.Combine(outputDir, "folder_classifications.csv"),
            new[]
            {
                "root_share", "relative_path", "path", "classification", "confidence", "reasons",
                "heuristic_classification", "heuristic_confidence", "heuristic_reasons",
                "size_bytes", "size_gib", "size_mib", "file_count", "dir_count",
                "prproj_count", "aep_count", "video_count", "audio_count", "image_count",
                "immediate_prproj_count", "immediate_cache_folder_count",
                "marker_kind", "marker_confirmed_by", "marker_confirmed_at", "marker_legacy_client", "marker_legacy_project_name",
                "marker_status", "marker_notes", "marker_tags"
            },
            report.Classifications
                .OrderBy(item => item.Path)
                .Select(item => new List<string?>
                {
                    rootShare,
                    item.RelativePath,
                    item.Path,
                    item.Classification,
                    item.Confidence.ToString("0.###"),
                    FormatReasons(item.Reasons),
                    item.HeuristicClassification,
                    item.HeuristicConfidence.ToString("0.###"),
                    FormatReasons(item.HeuristicReasons),
                    item.SizeBytes.ToString(),
                    ToGiB(item.SizeBytes),
                    ToMiB(item.SizeBytes),
                    item.FileCount.ToString(),
                    item.DirCount.ToString(),
                    item.PrprojCount.ToString(),
                    item.AepCount.ToString(),
                    item.VideoCount.ToString(),
                    item.AudioCount.ToString(),
                    item.ImageCount.ToString(),
                    item.ImmediatePrprojCount.ToString(),
                    (item.ImmediateAutoSaveFolderCount + item.ImmediateAudioPreviewFolderCount + item.ImmediateVideoPreviewFolderCount).ToString(),
                    item.Marker?.Kind ?? string.Empty,
                    item.Marker?.ConfirmedBy ?? string.Empty,
                    item.Marker?.ConfirmedAt ?? string.Empty,
                    item.Marker?.LegacyClient ?? string.Empty,
                    item.Marker?.LegacyProjectName ?? string.Empty,
                    item.Marker?.Status ?? string.Empty,
                    item.Marker?.Notes ?? string.Empty,
                    item.Marker?.Tags is null ? string.Empty : string.Join(";", item.Marker.Tags)
                }));

        CsvWriter.Write(Path.Combine(outputDir, "top_dirs.csv"),
            new[] { "root_share", "relative_path", "path", "size_bytes", "size_gib", "size_mib", "file_count", "dir_count", "prproj_count", "aep_count", "video_count", "audio_count", "image_count" },
            report.TopDirectories
                .Select(item => new List<string?>
                {
                    rootShare,
                    Path.GetRelativePath(report.ScanInfo.RootPath, item.Path),
                    item.Path,
                    item.SizeBytes.ToString(),
                    ToGiB(item.SizeBytes),
                    ToMiB(item.SizeBytes),
                    item.FileCount.ToString(),
                    item.DirCount.ToString(),
                    item.PrprojCount.ToString(),
                    item.AepCount.ToString(),
                    item.VideoCount.ToString(),
                    item.AudioCount.ToString(),
                    item.ImageCount.ToString()
                }));

        CsvWriter.Write(Path.Combine(outputDir, "top_files.csv"),
            new[] { "root_share", "relative_path", "path", "size_bytes", "size_gib", "size_mib", "extension" },
            report.TopFiles
                .Select(item => new List<string?>
                {
                    rootShare,
                    Path.GetRelativePath(report.ScanInfo.RootPath, item.Path),
                    item.Path,
                    item.SizeBytes.ToString(),
                    ToGiB(item.SizeBytes),
                    ToMiB(item.SizeBytes),
                    item.Extension
                }));

        CsvWriter.Write(Path.Combine(outputDir, "files_by_ext.csv"),
            new[] { "extension", "count", "total_bytes", "total_gib", "total_mib" },
            report.FilesByExtension
                .Select(item => new List<string?>
                {
                    item.Extension,
                    item.Count.ToString(),
                    item.TotalBytes.ToString(),
                    ToGiB(item.TotalBytes),
                    ToMiB(item.TotalBytes)
                }));

        CsvWriter.Write(Path.Combine(outputDir, "dup_candidates.csv"),
            new[] { "dup_type", "match_basis", "group_key", "count", "size_bytes", "size_gib", "size_mib", "last_write_local", "paths" },
            report.DuplicateCandidates
                .OrderByDescending(item => item.Count)
                .ThenByDescending(item => item.SizeBytes)
                .Select(item => new List<string?>
                {
                    item.DupType,
                    item.MatchBasis,
                    item.GroupKey,
                    item.Count.ToString(),
                    item.SizeBytes.ToString(),
                    ToGiB(item.SizeBytes),
                    ToMiB(item.SizeBytes),
                    FormatLocalTimestamp(item.LastWriteLocal),
                    string.Join(";", item.Paths.Select(path => FormatDuplicatePath(report.ScanInfo.RootPath, path)))
                }));
    }
    private static string FormatReasons(IReadOnlyList<string> reasons)
    {
        return reasons.Count == 0 ? string.Empty : string.Join(";", reasons);
    }

    private static int GetTriagePriority(ClassificationResult item, bool hasDuplicateFingerprint, int cacheCount)
    {
        var priority = item.Classification switch
        {
            "template_pack" => 1,
            "camera_dump_subtree" => 2,
            "cache_only" => 3,
            "unknown_needs_review" => 4,
            "empty_folder" => 5,
            "project_root" => 6,
            "project_container" => 7,
            "project_confirmed" => 8,
            "container_confirmed" => 8,
            _ => 9
        };

        if (item.ImmediatePrprojCount > 0)
        {
            priority -= 1;
        }

        if (cacheCount > 0)
        {
            priority -= 1;
        }

        if (hasDuplicateFingerprint)
        {
            priority -= 1;
        }

        return Math.Max(1, priority);
    }

    private static string ToGiB(long bytes)
    {
        return Math.Round(bytes / 1024d / 1024d / 1024d, 3).ToString("0.###");
    }

    private static string ToMiB(long bytes)
    {
        return Math.Round(bytes / 1024d / 1024d, 3).ToString("0.###");
    }

    private static string FormatLocalTimestamp(DateTime? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
    }

    private static string FormatDuplicatePath(string rootPath, DuplicatePathEntry entry)
    {
        var relative = Path.GetRelativePath(rootPath, entry.Path);
        if (entry.LastWriteLocal is null)
        {
            return relative;
        }

        return $"{relative} ({entry.LastWriteLocal.Value:yyyy-MM-dd HH:mm})";
    }

    private static IEnumerable<IReadOnlyList<string?>> BuildCacheRows(ScanReport report, string rootShare)
    {
        var countsByParent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in report.CacheFolders)
        {
            var parent = Directory.GetParent(entry.Path)?.FullName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(parent))
            {
                continue;
            }

            countsByParent[parent] = countsByParent.TryGetValue(parent, out var count) ? count + 1 : 1;
        }

        foreach (var entry in report.CacheFolders.OrderBy(item => item.Path))
        {
            var parent = Directory.GetParent(entry.Path)?.FullName ?? string.Empty;
            var parentRelative = string.IsNullOrWhiteSpace(parent)
                ? string.Empty
                : Path.GetRelativePath(report.ScanInfo.RootPath, parent);

            yield return new List<string?>
            {
                rootShare,
                Path.GetRelativePath(report.ScanInfo.RootPath, entry.Path),
                entry.Path,
                entry.Type,
                parent,
                parentRelative,
                countsByParent.TryGetValue(parent, out var count) ? count.ToString() : "0"
            };
        }
    }

    private static void WriteSummary(ScanReport report, string path)
    {
        var lines = new List<string>
        {
            $"Root: {report.ScanInfo.RootPath}",
            $"Profile: {report.ScanInfo.Profile}",
            $"Started: {report.ScanInfo.StartedUtc:u}",
            $"Finished: {report.ScanInfo.FinishedUtc:u}",
            $"DurationMs: {report.ScanInfo.DurationMs}",
            $"TotalFiles: {report.Inventory.TotalFiles}",
            $"TotalDirectories: {report.Inventory.TotalDirectories}",
            $"TotalBytes: {report.Inventory.TotalBytes}",
            $"PrprojCount: {report.Inventory.PrprojCount}",
            $"AepCount: {report.Inventory.AepCount}",
            $"VideoCount: {report.Inventory.VideoCount}",
            $"AudioCount: {report.Inventory.AudioCount}",
            $"ImageCount: {report.Inventory.ImageCount}",
            $"AutoSaveFolders: {report.Inventory.AutoSaveFolderCount}",
            $"AudioPreviewFolders: {report.Inventory.AudioPreviewFolderCount}",
            $"VideoPreviewFolders: {report.Inventory.VideoPreviewFolderCount}",
            $"Classifications: {report.Classifications.Count}",
            $"Errors: {report.Errors.Count}"
        };

        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }
}
