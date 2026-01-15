
using System.Diagnostics;
using System.Text.Json;
using MGF.LegacyAuditCli.Models;

namespace MGF.LegacyAuditCli.Scanning;

internal sealed class LegacyScanner
{
    private static readonly string[] MarkerFileNames =
    {
        "_mgf_project.tag.json",
        "_MGF_PROJECT.tag.json"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mov", ".mp4", ".mxf", ".avi", ".mkv", ".r3d", ".braw", ".ari", ".xf-avc"
    };

    private static readonly HashSet<string> RawVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".braw", ".r3d", ".ari"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".aif", ".aiff", ".flac", ".m4a"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".gif", ".psd", ".ai", ".eps"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z"
    };

    private static readonly HashSet<string> TemplateExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mogrt"
    };

    private static readonly HashSet<string> CameraCardFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PRIVATE", "AVCHD", "DCIM", "XDROOT", "CANON", "SONY", "CONTENTS", "BPAV", "CLIPS", "A001"
    };

    private static readonly string[] TemplateKeywords =
    {
        "template", "templates", "preset", "presets", "pack", "course", "tutorial", "lesson", "sample",
        "demo", "practice", "skillshare", "udemy", "motionarray", "envato", "videohive", "templatefx"
    };

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ScanReport Scan(ScanOptions options, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.RootPath))
        {
            throw new DirectoryNotFoundException($"Root path not found: {options.RootPath}");
        }

        var stopwatch = Stopwatch.StartNew();
        var started = DateTimeOffset.UtcNow;

        var excludes = ScanProfileRules.GetDefaultExcludes(options.Profile);
        var aggregates = new Dictionary<string, DirectoryAggregate>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<ScanError>();
        var extensionStats = new Dictionary<string, ExtensionStat>(StringComparer.OrdinalIgnoreCase);
        var duplicateFiles = new Dictionary<FileDupKey, List<string>>(new FileDupKeyComparer());
        var topFiles = new List<FileStat>();
        var fileWriteTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var cacheFolders = new List<CacheFolderEntry>();
        var autoSaveFolderCount = 0L;
        var audioPreviewFolderCount = 0L;
        var videoPreviewFolderCount = 0L;
        var totalDirectories = 0L;
        var totalFiles = 0L;
        var totalBytes = 0L;
        var totalPrproj = 0L;
        var totalAep = 0L;
        var totalVideo = 0L;
        var totalAudio = 0L;
        var totalImage = 0L;

        var optionsEnum = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        var stack = new Stack<DirFrame>();
        stack.Push(new DirFrame(options.RootPath, 0, false));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = stack.Pop();

            if (!frame.Exit)
            {
                var currentPath = frame.Path;
                if (frame.Depth > 0 && IsExcluded(Path.GetFileName(currentPath), excludes))
                {
                    continue;
                }

                if (IsReparsePoint(currentPath))
                {
                    continue;
                }

                var aggregate = new DirectoryAggregate(currentPath, frame.Depth)
                {
                    LastWriteLocal = new DirectoryInfo(currentPath).LastWriteTime
                };
                aggregates[currentPath] = aggregate;
                totalDirectories++;

                stack.Push(new DirFrame(currentPath, frame.Depth, true));

                try
                {
                    var directoryInfo = new DirectoryInfo(currentPath);
                    foreach (var entry in directoryInfo.EnumerateFileSystemInfos("*", optionsEnum))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (entry is DirectoryInfo dir)
                        {
                            if (IsReparsePoint(dir.FullName))
                            {
                                continue;
                            }

                            if (IsExcluded(dir.Name, excludes))
                            {
                                continue;
                            }

                            aggregate.ImmediateDirCount++;
                            aggregate.ChildDirs.Add(dir.FullName);
                            aggregate.FingerprintEntries.Add($"D|{dir.Name}");

                            if (IsCameraCardFolderName(dir.Name))
                            {
                                aggregate.CameraFolderMatches.Add(dir.Name);
                            }

                            if (IsAutosaveFolderName(dir.Name))
                            {
                                aggregate.ImmediateAutoSaveFolderCount++;
                                autoSaveFolderCount++;
                                cacheFolders.Add(new CacheFolderEntry { Path = dir.FullName, Type = "autosave" });
                            }

                            if (IsAudioPreviewFolderName(dir.Name))
                            {
                                aggregate.ImmediateAudioPreviewFolderCount++;
                                audioPreviewFolderCount++;
                                cacheFolders.Add(new CacheFolderEntry { Path = dir.FullName, Type = "audio_previews" });
                            }

                            if (IsVideoPreviewFolderName(dir.Name))
                            {
                                aggregate.ImmediateVideoPreviewFolderCount++;
                                videoPreviewFolderCount++;
                                cacheFolders.Add(new CacheFolderEntry { Path = dir.FullName, Type = "video_previews" });
                            }

                            if (IsRendersFolderName(dir.Name))
                            {
                                aggregate.ImmediateHasRendersFolder = true;
                            }

                            if (IsExportsFolderName(dir.Name))
                            {
                                aggregate.ImmediateHasExportsFolder = true;
                            }

                            if (IsGraphicsFolderName(dir.Name))
                            {
                                aggregate.ImmediateHasGraphicsFolder = true;
                            }

                            if (IsAudioFolderName(dir.Name))
                            {
                                aggregate.ImmediateHasAudioFolder = true;
                            }

                            if (IsFootageFolderName(dir.Name))
                            {
                                aggregate.ImmediateHasFootageFolder = true;
                            }

                            if (IsProjectFilesFolderName(dir.Name))
                            {
                                aggregate.ImmediateHasProjectFilesFolder = true;
                            }

                            if (options.MaxDepth < 0 || frame.Depth + 1 <= options.MaxDepth)
                            {
                                stack.Push(new DirFrame(dir.FullName, frame.Depth + 1, false));
                            }
                        }
                        else if (entry is FileInfo file)
                        {
                            aggregate.ImmediateFileCount++;
                            var size = SafeGetLength(file, errors);
                            aggregate.ImmediateSizeBytes += size;
                            totalFiles++;
                            totalBytes += size;

                            var ext = file.Extension;
                            UpdateExtensionStats(extensionStats, ext, size);

                            if (ext.Equals(".prproj", StringComparison.OrdinalIgnoreCase))
                            {
                                aggregate.ImmediatePrprojCount++;
                                totalPrproj++;
                            }

                            if (ext.Equals(".aep", StringComparison.OrdinalIgnoreCase))
                            {
                                aggregate.ImmediateAepCount++;
                                totalAep++;
                            }

                            if (VideoExtensions.Contains(ext))
                            {
                                aggregate.ImmediateVideoCount++;
                                totalVideo++;
                            }

                            if (RawVideoExtensions.Contains(ext))
                            {
                                aggregate.ImmediateRawVideoCount++;
                            }

                            if (AudioExtensions.Contains(ext))
                            {
                                aggregate.ImmediateAudioCount++;
                                totalAudio++;
                            }

                            if (ImageExtensions.Contains(ext))
                            {
                                aggregate.ImmediateImageCount++;
                                totalImage++;
                            }

                            if (ArchiveExtensions.Contains(ext))
                            {
                                aggregate.ImmediateZipCount++;
                            }

                            if (TemplateExtensions.Contains(ext))
                            {
                                aggregate.ImmediateMogrtCount++;
                            }

                            if (ext.Equals(".prpreset", StringComparison.OrdinalIgnoreCase) || ext.Equals(".prfpset", StringComparison.OrdinalIgnoreCase))
                            {
                                aggregate.ImmediatePresetCount++;
                            }

                            if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                aggregate.ImmediatePdfCount++;
                            }

                            aggregate.FingerprintEntries.Add($"F|{file.Name}|{size}");
                            UpdateTopFiles(topFiles, file.FullName, size, ext);
                            TrackDuplicateFile(duplicateFiles, file.Name, size, file.FullName);
                            fileWriteTimes[file.FullName] = file.LastWriteTime;

                            if (IsMarkerFileName(file.Name))
                            {
                                TryReadMarker(file.FullName, aggregate, errors, _jsonOptions);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    aggregate.HasScanError = true;
                    AddError(errors, currentPath, ex);
                }

                aggregate.Fingerprint = DirectoryFingerprint.Compute(aggregate.FingerprintEntries);
            }
            else
            {
                if (!aggregates.TryGetValue(frame.Path, out var aggregate))
                {
                    continue;
                }

                aggregate.TotalSizeBytes = aggregate.ImmediateSizeBytes;
                aggregate.FileCount = aggregate.ImmediateFileCount;
                aggregate.DirCount = aggregate.ImmediateDirCount;
                aggregate.PrprojCount = aggregate.ImmediatePrprojCount;
                aggregate.AepCount = aggregate.ImmediateAepCount;
                aggregate.VideoCount = aggregate.ImmediateVideoCount;
                aggregate.AudioCount = aggregate.ImmediateAudioCount;
                aggregate.ImageCount = aggregate.ImmediateImageCount;
                aggregate.RawVideoCount = aggregate.ImmediateRawVideoCount;
                aggregate.ZipCount = aggregate.ImmediateZipCount;
                aggregate.MogrtCount = aggregate.ImmediateMogrtCount;
                aggregate.PresetCount = aggregate.ImmediatePresetCount;
                aggregate.PdfCount = aggregate.ImmediatePdfCount;
                aggregate.AutoSaveFolderCount = aggregate.ImmediateAutoSaveFolderCount;
                aggregate.AudioPreviewFolderCount = aggregate.ImmediateAudioPreviewFolderCount;
                aggregate.VideoPreviewFolderCount = aggregate.ImmediateVideoPreviewFolderCount;
                aggregate.HasAutoSaveFolder = aggregate.ImmediateAutoSaveFolderCount > 0;
                aggregate.HasAudioPreviewFolder = aggregate.ImmediateAudioPreviewFolderCount > 0;
                aggregate.HasVideoPreviewFolder = aggregate.ImmediateVideoPreviewFolderCount > 0;
                aggregate.HasRendersFolder = aggregate.ImmediateHasRendersFolder;
                aggregate.HasExportsFolder = aggregate.ImmediateHasExportsFolder;
                aggregate.HasGraphicsFolder = aggregate.ImmediateHasGraphicsFolder;
                aggregate.HasAudioFolder = aggregate.ImmediateHasAudioFolder;
                aggregate.HasFootageFolder = aggregate.ImmediateHasFootageFolder;
                aggregate.HasProjectFilesFolder = aggregate.ImmediateHasProjectFilesFolder;

                foreach (var childPath in aggregate.ChildDirs)
                {
                    if (!aggregates.TryGetValue(childPath, out var child))
                    {
                        continue;
                    }

                    aggregate.TotalSizeBytes += child.TotalSizeBytes;
                    aggregate.FileCount += child.FileCount;
                    aggregate.DirCount += child.DirCount;
                    aggregate.PrprojCount += child.PrprojCount;
                    aggregate.AepCount += child.AepCount;
                    aggregate.VideoCount += child.VideoCount;
                    aggregate.AudioCount += child.AudioCount;
                    aggregate.ImageCount += child.ImageCount;
                    aggregate.RawVideoCount += child.RawVideoCount;
                    aggregate.ZipCount += child.ZipCount;
                    aggregate.MogrtCount += child.MogrtCount;
                    aggregate.PresetCount += child.PresetCount;
                    aggregate.PdfCount += child.PdfCount;
                    aggregate.AutoSaveFolderCount += child.AutoSaveFolderCount;
                    aggregate.AudioPreviewFolderCount += child.AudioPreviewFolderCount;
                    aggregate.VideoPreviewFolderCount += child.VideoPreviewFolderCount;
                    aggregate.HasAutoSaveFolder |= child.HasAutoSaveFolder;
                    aggregate.HasAudioPreviewFolder |= child.HasAudioPreviewFolder;
                    aggregate.HasVideoPreviewFolder |= child.HasVideoPreviewFolder;
                    aggregate.HasRendersFolder |= child.HasRendersFolder;
                    aggregate.HasExportsFolder |= child.HasExportsFolder;
                    aggregate.HasGraphicsFolder |= child.HasGraphicsFolder;
                    aggregate.HasAudioFolder |= child.HasAudioFolder;
                    aggregate.HasFootageFolder |= child.HasFootageFolder;
                    aggregate.HasProjectFilesFolder |= child.HasProjectFilesFolder;
                }

                aggregate.FingerprintEntries.Clear();
            }
        }

        var finished = DateTimeOffset.UtcNow;
        stopwatch.Stop();

        var duplicateFolderGroups = BuildDuplicateFolderGroups(aggregates.Values);
        var repeatedFingerprints = duplicateFolderGroups.Select(group => group.Fingerprint).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var classifications = BuildClassifications(aggregates, options.RootPath, repeatedFingerprints);
        var duplicateFileCandidates = duplicateFiles
            .Where(pair => pair.Value.Count > 1)
            .Select(pair =>
            {
                var lastWrite = pair.Value
                    .Select(path => fileWriteTimes.TryGetValue(path, out var value) ? value : (DateTime?)null)
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .DefaultIfEmpty()
                    .Max();

                return new DuplicateCandidate
                {
                    DupType = "file",
                    MatchBasis = "name+size",
                    GroupKey = $"{pair.Key.Name}|{pair.Key.Size}",
                    Count = pair.Value.Count,
                    SizeBytes = pair.Key.Size,
                    LastWriteLocal = lastWrite == default ? null : lastWrite,
                    Paths = pair.Value
                        .Select(path => new DuplicatePathEntry
                        {
                            Path = path,
                            LastWriteLocal = fileWriteTimes.TryGetValue(path, out var value) ? value : null
                        })
                        .ToList()
                };
            })
            .ToList();

        var duplicateFolderCandidates = duplicateFolderGroups
            .Select(group =>
            {
                var lastWrite = group.Paths
                    .Select(path => aggregates.TryGetValue(path, out var agg) ? agg.LastWriteLocal : null)
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .DefaultIfEmpty()
                    .Max();

                return new DuplicateCandidate
                {
                    DupType = "folder",
                    MatchBasis = "fingerprint",
                    GroupKey = group.Fingerprint,
                    Count = group.Paths.Count,
                    SizeBytes = GetLargestDirectorySize(aggregates, group.Paths),
                    LastWriteLocal = lastWrite == default ? null : lastWrite,
                    Paths = group.Paths
                        .Select(path => new DuplicatePathEntry
                        {
                            Path = path,
                            LastWriteLocal = aggregates.TryGetValue(path, out var agg) ? agg.LastWriteLocal : null
                        })
                        .ToList()
                };
            })
            .ToList();

        var duplicateCandidates = duplicateFileCandidates
            .Concat(duplicateFolderCandidates)
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.SizeBytes)
            .ToList();

        var topDirectories = aggregates.Values
            .OrderByDescending(item => item.TotalSizeBytes)
            .Take(200)
            .Select(item => new DirectoryStat
            {
                Path = item.Path,
                SizeBytes = item.TotalSizeBytes,
                FileCount = item.FileCount,
                DirCount = item.DirCount,
                PrprojCount = item.PrprojCount,
                AepCount = item.AepCount,
                VideoCount = item.VideoCount,
                AudioCount = item.AudioCount,
                ImageCount = item.ImageCount
            })
            .ToList();

        var report = new ScanReport
        {
            ScanInfo = new ScanInfo
            {
                RootPath = options.RootPath,
                OutputPath = options.OutputPath,
                Profile = options.Profile.ToString().ToLowerInvariant(),
                StartedUtc = started,
                FinishedUtc = finished,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ToolVersion = typeof(LegacyScanner).Assembly.GetName().Version?.ToString() ?? "unknown"
            },
            Inventory = new InventorySummary
            {
                TotalFiles = totalFiles,
                TotalDirectories = totalDirectories,
                TotalBytes = totalBytes,
                PrprojCount = totalPrproj,
                AepCount = totalAep,
                VideoCount = totalVideo,
                AudioCount = totalAudio,
                ImageCount = totalImage,
                AutoSaveFolderCount = autoSaveFolderCount,
                AudioPreviewFolderCount = audioPreviewFolderCount,
                VideoPreviewFolderCount = videoPreviewFolderCount
            },
            FilesByExtension = extensionStats.Values.OrderByDescending(item => item.Count).ToList(),
            TopDirectories = topDirectories,
            TopFiles = topFiles.OrderByDescending(item => item.SizeBytes).ToList(),
            Classifications = classifications,
            DuplicateCandidates = duplicateCandidates,
            CacheFolders = cacheFolders,
            Errors = errors
        };

        return report;
    }
    private static bool IsExcluded(string name, IReadOnlyList<string> excludes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var exclude in excludes)
        {
            if (name.Contains(exclude, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMarkerFileName(string name)
    {
        return MarkerFileNames.Any(marker => name.Equals(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static void TryReadMarker(string path, DirectoryAggregate aggregate, List<ScanError> errors, JsonSerializerOptions jsonOptions)
    {
        try
        {
            var json = File.ReadAllText(path);
            var marker = JsonSerializer.Deserialize<MarkerFile>(json, jsonOptions);
            if (marker is not null)
            {
                aggregate.Marker = marker;
            }
        }
        catch (Exception ex)
        {
            AddError(errors, path, ex);
        }
    }

    private static long SafeGetLength(FileInfo file, List<ScanError> errors)
    {
        try
        {
            return file.Length;
        }
        catch (Exception ex)
        {
            AddError(errors, file.FullName, ex);
            return 0;
        }
    }

    private static void UpdateExtensionStats(Dictionary<string, ExtensionStat> stats, string extension, long size)
    {
        var key = string.IsNullOrWhiteSpace(extension) ? "(none)" : extension.ToLowerInvariant();
        if (!stats.TryGetValue(key, out var entry))
        {
            entry = new ExtensionStat { Extension = key };
            stats[key] = entry;
        }

        entry.Count += 1;
        entry.TotalBytes += size;
    }

    private static void UpdateTopFiles(List<FileStat> topFiles, string path, long size, string extension)
    {
        const int max = 200;
        if (topFiles.Count < max)
        {
            topFiles.Add(new FileStat { Path = path, SizeBytes = size, Extension = extension });
            return;
        }

        var min = topFiles.Min(item => item.SizeBytes);
        if (size <= min)
        {
            return;
        }

        topFiles.Add(new FileStat { Path = path, SizeBytes = size, Extension = extension });
        topFiles.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
        if (topFiles.Count > max)
        {
            topFiles.RemoveRange(max, topFiles.Count - max);
        }
    }

    private static void TrackDuplicateFile(Dictionary<FileDupKey, List<string>> dupes, string name, long size, string path)
    {
        var key = new FileDupKey(name, size);
        if (!dupes.TryGetValue(key, out var list))
        {
            list = new List<string>();
            dupes[key] = list;
        }

        list.Add(path);
    }


    private static bool IsCameraCardFolderName(string name)
    {
        return CameraCardFolderNames.Contains(name);
    }

    private static bool IsAutosaveFolderName(string name)
    {
        return name.Contains("auto-save", StringComparison.OrdinalIgnoreCase)
            || name.Contains("autosave", StringComparison.OrdinalIgnoreCase)
            || name.Contains("auto save", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAudioPreviewFolderName(string name)
    {
        return name.Contains("audio preview", StringComparison.OrdinalIgnoreCase)
            || name.Contains("audio previews", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVideoPreviewFolderName(string name)
    {
        return name.Contains("video preview", StringComparison.OrdinalIgnoreCase)
            || name.Contains("video previews", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRendersFolderName(string name)
    {
        return name.Contains("render", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExportsFolderName(string name)
    {
        return name.Contains("export", StringComparison.OrdinalIgnoreCase) || name.Contains("deliverable", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGraphicsFolderName(string name)
    {
        return name.Contains("graphic", StringComparison.OrdinalIgnoreCase) || name.Contains("gfx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAudioFolderName(string name)
    {
        return name.Contains("audio", StringComparison.OrdinalIgnoreCase) || name.Contains("sound", StringComparison.OrdinalIgnoreCase) || name.Contains("music", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFootageFolderName(string name)
    {
        return name.Contains("footage", StringComparison.OrdinalIgnoreCase) || name.Contains("media", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjectFilesFolderName(string name)
    {
        return name.Contains("project", StringComparison.OrdinalIgnoreCase) || name.Contains("edit", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddError(List<ScanError> errors, string path, Exception ex)
    {
        errors.Add(new ScanError
        {
            Path = path,
            ErrorType = ex.GetType().Name,
            ErrorCode = ex.HResult,
            Message = ex.Message
        });
    }

    private static List<ClassificationResult> BuildClassifications(
        Dictionary<string, DirectoryAggregate> aggregates,
        string rootPath,
        HashSet<string> repeatedFingerprints)
    {
        var results = new List<ClassificationResult>();
        var scoresByPath = new Dictionary<string, HeuristicScores>(StringComparer.OrdinalIgnoreCase);

        foreach (var aggregate in aggregates.Values)
        {
            var repeated = aggregate.Fingerprint is not null && repeatedFingerprints.Contains(aggregate.Fingerprint);
            scoresByPath[aggregate.Path] = Heuristics.Evaluate(aggregate, repeated, TemplateKeywords);
        }

        var projectRootCandidates = scoresByPath
            .Where(pair => pair.Value.ProjectRootScore >= Heuristics.ProjectRootThreshold)
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var containerCandidates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var aggregate in aggregates.Values)
        {
            if (aggregate.Path.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var childRoots = aggregate.ChildDirs.Count(child => projectRootCandidates.Contains(child));
            if (childRoots >= 2 && scoresByPath[aggregate.Path].ProjectRootScore < Heuristics.ProjectRootStrongThreshold)
            {
                containerCandidates[aggregate.Path] = childRoots;
            }
        }

        foreach (var aggregate in aggregates.Values)
        {
            if (aggregate.HasScanError && aggregate.Marker is null)
            {
                continue;
            }

            if (aggregate.Path.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!scoresByPath.TryGetValue(aggregate.Path, out var scores))
            {
                continue;
            }

            var isContainerCandidate = containerCandidates.ContainsKey(aggregate.Path);
            if (!scores.HasSignals && aggregate.Marker is null && !scores.IsEmpty && !isContainerCandidate)
            {
                continue;
            }

            var classification = scores.IsEmpty ? "empty_folder" : "unknown_needs_review";
            var confidence = scores.IsEmpty ? 1.0 : scores.UnknownScore;
            var reasons = scores.IsEmpty ? new List<string> { "empty_folder" } : scores.UnknownReasons;

            if (!scores.IsEmpty)
            {
                if (scores.TemplateScore >= Heuristics.TemplateThreshold)
                {
                    classification = "template_pack";
                    confidence = scores.TemplateScore;
                    reasons = scores.TemplateReasons;
                }
                else if (scores.CameraDumpScore >= Heuristics.CameraDumpThreshold)
                {
                    classification = "camera_dump_subtree";
                    confidence = scores.CameraDumpScore;
                    reasons = scores.CameraDumpReasons;
                }
                else if (scores.ProjectRootScore >= Heuristics.ProjectRootThreshold)
                {
                    classification = "project_root";
                    confidence = scores.ProjectRootScore;
                    reasons = scores.ProjectRootReasons;
                }
                else if (scores.CacheOnlyScore >= Heuristics.CacheOnlyThreshold)
                {
                    classification = "cache_only";
                    confidence = scores.CacheOnlyScore;
                    reasons = scores.CacheOnlyReasons;
                }
            }

            if (classification == "unknown_needs_review" && !scores.HasSignals)
            {
                continue;
            }

            var heuristicClassification = classification;
            var heuristicConfidence = confidence;
            var heuristicReasons = new List<string>(reasons);

            if (containerCandidates.TryGetValue(aggregate.Path, out var childRootCount)
                && classification != "project_root")
            {
                classification = "project_container";
                confidence = Math.Clamp(0.6 + 0.1 * childRootCount, 0, 1);
                reasons = new List<string> { $"child_project_roots={childRootCount}" };
            }

            if (aggregate.Marker is not null)
            {
                var markerKind = aggregate.Marker.Kind?.Trim().ToLowerInvariant();
                if (markerKind == "container")
                {
                    classification = "container_confirmed";
                    confidence = 1.0;
                    reasons = new List<string> { "marker_file_present" };
                }
                else
                {
                    classification = "project_confirmed";
                    confidence = 1.0;
                    reasons = new List<string> { "marker_file_present" };
                }
            }

            results.Add(new ClassificationResult
            {
                Path = aggregate.Path,
                RelativePath = Path.GetRelativePath(rootPath, aggregate.Path),
                RootShare = PathHelpers.GetRootShare(rootPath),
                Classification = classification,
                Confidence = Math.Round(confidence, 3),
                Reasons = reasons,
                HeuristicClassification = heuristicClassification,
                HeuristicConfidence = Math.Round(heuristicConfidence, 3),
                HeuristicReasons = heuristicReasons,
                ProjectRootConfidence = Math.Round(scores.ProjectRootScore, 3),
                ProjectRootReasons = scores.ProjectRootReasons,
                ProjectContainerConfidence = containerCandidates.ContainsKey(aggregate.Path)
                    ? Math.Round(Math.Clamp(0.6 + 0.1 * containerCandidates[aggregate.Path], 0, 1), 3)
                    : 0,
                ProjectContainerReasons = containerCandidates.ContainsKey(aggregate.Path)
                    ? new List<string> { $"child_project_roots={containerCandidates[aggregate.Path]}" }
                    : new List<string>(),
                TemplatePackConfidence = Math.Round(scores.TemplateScore, 3),
                TemplatePackReasons = scores.TemplateReasons,
                CameraDumpConfidence = Math.Round(scores.CameraDumpScore, 3),
                CameraDumpReasons = scores.CameraDumpReasons,
                CacheOnlyConfidence = Math.Round(scores.CacheOnlyScore, 3),
                CacheOnlyReasons = scores.CacheOnlyReasons,
                UnknownConfidence = Math.Round(scores.UnknownScore, 3),
                UnknownReasons = scores.UnknownReasons,
                FolderFingerprint = aggregate.Fingerprint,
                SizeBytes = aggregate.TotalSizeBytes,
                FileCount = aggregate.FileCount,
                DirCount = aggregate.DirCount,
                PrprojCount = aggregate.PrprojCount,
                AepCount = aggregate.AepCount,
                VideoCount = aggregate.VideoCount,
                AudioCount = aggregate.AudioCount,
                ImageCount = aggregate.ImageCount,
                ImmediatePrprojCount = aggregate.ImmediatePrprojCount,
                ImmediateAepCount = aggregate.ImmediateAepCount,
                ImmediateVideoCount = aggregate.ImmediateVideoCount,
                ImmediateAudioCount = aggregate.ImmediateAudioCount,
                ImmediateImageCount = aggregate.ImmediateImageCount,
                ImmediateRawVideoCount = aggregate.ImmediateRawVideoCount,
                ImmediateAutoSaveFolderCount = aggregate.ImmediateAutoSaveFolderCount,
                ImmediateAudioPreviewFolderCount = aggregate.ImmediateAudioPreviewFolderCount,
                ImmediateVideoPreviewFolderCount = aggregate.ImmediateVideoPreviewFolderCount,
                Marker = aggregate.Marker
            });
        }

        foreach (var result in results.Where(item => item.Classification == "camera_dump_subtree"))
        {
            var parentPath = Directory.GetParent(result.Path)?.FullName;
            if (parentPath is null)
            {
                continue;
            }

            if (containerCandidates.ContainsKey(parentPath))
            {
                result.LikelyProjectContainerPath = parentPath;
            }

            if (aggregates.TryGetValue(parentPath, out var parentAgg))
            {
                var siblingRoots = parentAgg.ChildDirs
                    .Where(child => projectRootCandidates.Contains(child))
                    .ToList();

                if (siblingRoots.Count > 0)
                {
                    result.LikelyProjectRootPath = siblingRoots[0];
                }
                else if (projectRootCandidates.Contains(parentPath))
                {
                    result.LikelyProjectRootPath = parentPath;
                }
            }
        }

        return results;
    }

    private static List<DuplicateFolderGroup> BuildDuplicateFolderGroups(IEnumerable<DirectoryAggregate> aggregates)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var aggregate in aggregates)
        {
            if (string.IsNullOrWhiteSpace(aggregate.Fingerprint))
            {
                continue;
            }

            if (!groups.TryGetValue(aggregate.Fingerprint, out var list))
            {
                list = new List<string>();
                groups[aggregate.Fingerprint] = list;
            }

            list.Add(aggregate.Path);
        }

        return groups
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => new DuplicateFolderGroup
            {
                Fingerprint = pair.Key,
                Paths = pair.Value
            })
            .OrderByDescending(group => group.Paths.Count)
            .ToList();
    }

    private static long GetLargestDirectorySize(Dictionary<string, DirectoryAggregate> aggregates, IReadOnlyList<string> paths)
    {
        long max = 0;
        foreach (var path in paths)
        {
            if (aggregates.TryGetValue(path, out var aggregate))
            {
                max = Math.Max(max, aggregate.TotalSizeBytes);
            }
        }

        return max;
    }

    private sealed class DuplicateFolderGroup
    {
        public string Fingerprint { get; init; } = string.Empty;
        public List<string> Paths { get; init; } = new();
    }

    private readonly record struct DirFrame(string Path, int Depth, bool Exit);

    private readonly record struct FileDupKey(string Name, long Size);

    private sealed class FileDupKeyComparer : IEqualityComparer<FileDupKey>
    {
        public bool Equals(FileDupKey x, FileDupKey y)
        {
            return x.Size == y.Size && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(FileDupKey obj)
        {
            return HashCode.Combine(obj.Size, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
        }
    }
}

internal static class PathHelpers
{
    public static string GetRootShare(string rootPath)
    {
        if (rootPath.StartsWith("\\\\", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = rootPath.Trim('\\');
            var parts = trimmed.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"\\\\{parts[0]}\\{parts[1]}";
            }
        }

        return Path.GetPathRoot(rootPath) ?? rootPath;
    }
}

