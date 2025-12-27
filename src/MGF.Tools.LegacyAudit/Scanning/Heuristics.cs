namespace MGF.Tools.LegacyAudit.Scanning;

internal sealed class HeuristicScores
{
    public bool IsEmpty { get; init; }
    public double ProjectRootScore { get; init; }
    public List<string> ProjectRootReasons { get; init; } = new();
    public double TemplateScore { get; init; }
    public List<string> TemplateReasons { get; init; } = new();
    public double CameraDumpScore { get; init; }
    public List<string> CameraDumpReasons { get; init; } = new();
    public double CacheOnlyScore { get; init; }
    public List<string> CacheOnlyReasons { get; init; } = new();
    public double UnknownScore { get; init; }
    public List<string> UnknownReasons { get; init; } = new();
    public bool HasSignals { get; init; }
}

internal static class Heuristics
{
    public const double TemplateThreshold = 0.65;
    public const double CameraDumpThreshold = 0.7;
    public const double ProjectRootThreshold = 0.65;
    public const double ProjectRootStrongThreshold = 0.8;
    public const double CacheOnlyThreshold = 0.75;

    public static HeuristicScores Evaluate(DirectoryAggregate agg, bool isRepeatedFingerprint, IReadOnlyList<string> templateKeywords)
    {
        if (agg.FileCount == 0 && agg.DirCount == 0)
        {
            return new HeuristicScores
            {
                IsEmpty = true,
                UnknownScore = 0,
                UnknownReasons = new List<string> { "empty_folder" },
                HasSignals = true
            };
        }

        var template = ScoreTemplate(agg, templateKeywords, isRepeatedFingerprint);
        var camera = ScoreCameraDump(agg);
        var cache = ScoreCacheOnly(agg);
        var project = ScoreProjectRoot(agg, template.Score);

        var unknownReasons = BuildUnknownReasons(agg, project.Score);
        var hasSignals = HasAnySignals(agg, template.Score, camera.Score, cache.Score, project.Score);

        return new HeuristicScores
        {
            IsEmpty = false,
            ProjectRootScore = project.Score,
            ProjectRootReasons = project.Reasons,
            TemplateScore = template.Score,
            TemplateReasons = template.Reasons,
            CameraDumpScore = camera.Score,
            CameraDumpReasons = camera.Reasons,
            CacheOnlyScore = cache.Score,
            CacheOnlyReasons = cache.Reasons,
            UnknownScore = hasSignals ? 0.2 : 0,
            UnknownReasons = unknownReasons,
            HasSignals = hasSignals
        };
    }

    private static HeuristicScore ScoreTemplate(DirectoryAggregate agg, IReadOnlyList<string> keywords, bool isRepeatedFingerprint)
    {
        var score = 0.0;
        var reasons = new List<string>();
        var matches = KeywordMatcher.FindMatches(agg.Path, keywords);
        if (matches.Count > 0)
        {
            score += 0.5;
            reasons.AddRange(matches.Select(match => $"template_keyword:{match}"));
        }

        if (agg.ImmediateMogrtCount >= 5 || agg.ImmediatePresetCount >= 5)
        {
            score += 0.25;
            reasons.Add("template_assets>=5");
        }

        if (agg.ImmediateZipCount >= 3)
        {
            score += 0.2;
            reasons.Add("archives>=3");
        }

        if (agg.ImmediateFileCount >= 50 && agg.ImmediateSizeBytes > 0)
        {
            var avgSize = (double)agg.ImmediateSizeBytes / agg.ImmediateFileCount;
            if (avgSize < 2 * 1024 * 1024)
            {
                score += 0.15;
                reasons.Add("small_files_avg");
            }
        }

        if (agg.ImmediateVideoCount == 0 && agg.ImmediateAudioCount == 0 && agg.ImmediateImageCount < 5)
        {
            score += 0.15;
            reasons.Add("low_media_density");
        }

        if (isRepeatedFingerprint)
        {
            score += 0.2;
            reasons.Add("repeated_structure");
        }

        return new HeuristicScore("template_pack", Math.Clamp(score, 0, 1), TemplateThreshold, reasons);
    }

    private static HeuristicScore ScoreCameraDump(DirectoryAggregate agg)
    {
        var score = 0.0;
        var reasons = new List<string>();
        if (agg.CameraFolderMatches.Count > 0)
        {
            score += 0.7;
            reasons.AddRange(agg.CameraFolderMatches.Select(match => $"camera_folder:{match}"));
        }

        if (agg.ImmediateRawVideoCount >= 5 && agg.ImmediateFileCount == agg.ImmediateRawVideoCount)
        {
            score += 0.8;
            reasons.Add("raw_video_only");
        }
        else if (agg.ImmediateRawVideoCount >= 10)
        {
            score += 0.4;
            reasons.Add("raw_video_count>=10");
        }

        if (agg.ImmediateVideoCount >= 20)
        {
            score += 0.1;
            reasons.Add("video_files>=20");
        }

        if (agg.ImmediateFileCount >= 200)
        {
            score += 0.1;
            reasons.Add("file_count>=200");
        }

        return new HeuristicScore("camera_dump_subtree", Math.Clamp(score, 0, 1), CameraDumpThreshold, reasons);
    }

    private static HeuristicScore ScoreProjectRoot(DirectoryAggregate agg, double templateScore)
    {
        var score = 0.0;
        var reasons = new List<string>();

        if (agg.ImmediatePrprojCount > 0)
        {
            score += 0.3;
            reasons.Add($"prproj_count={agg.ImmediatePrprojCount}");
        }

        if (agg.ImmediateAepCount > 0)
        {
            score += 0.15;
            reasons.Add($"aep_count={agg.ImmediateAepCount}");
        }

        if (agg.ImmediateAutoSaveFolderCount > 0)
        {
            score += 0.25;
            reasons.Add("autosave_folder");
        }

        if (agg.ImmediateAudioPreviewFolderCount > 0)
        {
            score += 0.15;
            reasons.Add("audio_previews_folder");
        }

        if (agg.ImmediateVideoPreviewFolderCount > 0)
        {
            score += 0.15;
            reasons.Add("video_previews_folder");
        }

        if (agg.ImmediateHasRendersFolder)
        {
            score += 0.15;
            reasons.Add("renders_folder");
        }

        if (agg.ImmediateHasExportsFolder)
        {
            score += 0.15;
            reasons.Add("exports_folder");
        }

        if (agg.ImmediateHasProjectFilesFolder)
        {
            score += 0.1;
            reasons.Add("project_files_folder");
        }

        if (agg.ImmediateHasFootageFolder)
        {
            score += 0.05;
            reasons.Add("footage_folder");
        }

        if (agg.ImmediateHasAudioFolder)
        {
            score += 0.05;
            reasons.Add("audio_folder");
        }

        if (agg.ImmediateHasGraphicsFolder)
        {
            score += 0.05;
            reasons.Add("graphics_folder");
        }

        if (agg.ImmediateVideoCount >= 5)
        {
            score += 0.15;
            reasons.Add("video_files>=5");
        }

        if (agg.ImmediateAudioCount >= 5)
        {
            score += 0.1;
            reasons.Add("audio_files>=5");
        }

        if (agg.ImmediateImageCount >= 10)
        {
            score += 0.05;
            reasons.Add("image_files>=10");
        }

        var mediaTypes = 0;
        if (agg.ImmediateVideoCount > 0) mediaTypes++;
        if (agg.ImmediateAudioCount > 0) mediaTypes++;
        if (agg.ImmediateImageCount > 0) mediaTypes++;
        if (mediaTypes >= 2)
        {
            score += 0.1;
            reasons.Add("media_mix");
        }

        if (templateScore >= TemplateThreshold)
        {
            score -= 0.25;
            reasons.Add("template_penalty");
        }

        score = Math.Clamp(score, 0, 1);
        return new HeuristicScore("project_root", score, ProjectRootThreshold, reasons);
    }

    private static HeuristicScore ScoreCacheOnly(DirectoryAggregate agg)
    {
        var score = 0.0;
        var reasons = new List<string>();

        if (agg.ImmediatePrprojCount > 0 || agg.ImmediateAepCount > 0
            || agg.ImmediateHasExportsFolder || agg.ImmediateHasRendersFolder
            || agg.ImmediateHasProjectFilesFolder || agg.ImmediateHasFootageFolder
            || agg.ImmediateHasAudioFolder || agg.ImmediateHasGraphicsFolder)
        {
            reasons.Add("project_structure_present");
            return new HeuristicScore("cache_only", 0, CacheOnlyThreshold, reasons);
        }

        var cacheFolders = agg.ImmediateAutoSaveFolderCount + agg.ImmediateAudioPreviewFolderCount + agg.ImmediateVideoPreviewFolderCount;
        if (cacheFolders > 0)
        {
            score += 0.7;
            reasons.Add("cache_folders_present");
        }

        if (agg.ImmediatePrprojCount == 0 && agg.ImmediateAepCount == 0)
        {
            score += 0.1;
            reasons.Add("no_project_files");
        }

        if (!agg.ImmediateHasExportsFolder && !agg.ImmediateHasRendersFolder && !agg.ImmediateHasProjectFilesFolder)
        {
            score += 0.1;
            reasons.Add("no_edit_folders");
        }

        if (agg.ImmediateVideoCount == 0 && agg.ImmediateAudioCount == 0 && agg.ImmediateImageCount == 0)
        {
            score += 0.1;
            reasons.Add("no_media_files");
        }

        return new HeuristicScore("cache_only", Math.Clamp(score, 0, 1), CacheOnlyThreshold, reasons);
    }

    private static List<string> BuildUnknownReasons(DirectoryAggregate agg, double projectScore)
    {
        var reasons = new List<string>();
        if (agg.ImmediatePrprojCount > 0 && projectScore < ProjectRootThreshold)
        {
            reasons.Add("prproj_without_structural_signals");
        }

        if (agg.ImmediateVideoCount > 0 || agg.ImmediateAudioCount > 0 || agg.ImmediateImageCount > 0)
        {
            reasons.Add("media_present_without_project_signals");
        }

        if (agg.ImmediateZipCount > 0)
        {
            reasons.Add("archives_present");
        }

        return reasons;
    }

    private static bool HasAnySignals(DirectoryAggregate agg, double templateScore, double cameraScore, double cacheScore, double projectScore)
    {
        if (templateScore > 0 || cameraScore > 0 || cacheScore > 0 || projectScore > 0)
        {
            return true;
        }

        return agg.PrprojCount > 0
            || agg.AepCount > 0
            || agg.VideoCount > 0
            || agg.AudioCount > 0
            || agg.ImageCount > 0
            || agg.AutoSaveFolderCount > 0
            || agg.AudioPreviewFolderCount > 0
            || agg.VideoPreviewFolderCount > 0
            || agg.ZipCount > 0
            || agg.MogrtCount > 0
            || agg.PresetCount > 0
            || agg.PdfCount > 0;
    }

    private sealed record HeuristicScore(string Classification, double Score, double Threshold, List<string> Reasons);
}

internal static class KeywordMatcher
{
    public static List<string> FindMatches(string path, IReadOnlyList<string> keywords)
    {
        var matches = new List<string>();
        var lower = path.ToLowerInvariant();
        foreach (var keyword in keywords)
        {
            if (lower.Contains(keyword))
            {
                matches.Add(keyword);
            }
        }

        return matches;
    }
}
