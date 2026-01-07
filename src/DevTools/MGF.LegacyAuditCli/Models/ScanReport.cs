namespace MGF.LegacyAuditCli.Models;

public sealed class ScanReport
{
    public ScanInfo ScanInfo { get; set; } = new();
    public InventorySummary Inventory { get; set; } = new();
    public List<ExtensionStat> FilesByExtension { get; set; } = new();
    public List<DirectoryStat> TopDirectories { get; set; } = new();
    public List<FileStat> TopFiles { get; set; } = new();
    public List<ClassificationResult> Classifications { get; set; } = new();
    public List<DuplicateCandidate> DuplicateCandidates { get; set; } = new();
    public List<CacheFolderEntry> CacheFolders { get; set; } = new();
    public List<ScanError> Errors { get; set; } = new();
}

public sealed class ScanInfo
{
    public string RootPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset FinishedUtc { get; set; }
    public long DurationMs { get; set; }
    public string ToolVersion { get; set; } = string.Empty;
}

public sealed class InventorySummary
{
    public long TotalFiles { get; set; }
    public long TotalDirectories { get; set; }
    public long TotalBytes { get; set; }
    public long PrprojCount { get; set; }
    public long AepCount { get; set; }
    public long VideoCount { get; set; }
    public long AudioCount { get; set; }
    public long ImageCount { get; set; }
    public long AutoSaveFolderCount { get; set; }
    public long AudioPreviewFolderCount { get; set; }
    public long VideoPreviewFolderCount { get; set; }
}

public sealed class ExtensionStat
{
    public string Extension { get; set; } = string.Empty;
    public long Count { get; set; }
    public long TotalBytes { get; set; }
}

public sealed class DirectoryStat
{
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long FileCount { get; set; }
    public long DirCount { get; set; }
    public long PrprojCount { get; set; }
    public long AepCount { get; set; }
    public long VideoCount { get; set; }
    public long AudioCount { get; set; }
    public long ImageCount { get; set; }
}

public sealed class FileStat
{
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Extension { get; set; } = string.Empty;
}

public sealed class ClassificationResult
{
    public string Path { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string RootShare { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> Reasons { get; set; } = new();

    public string HeuristicClassification { get; set; } = string.Empty;
    public double HeuristicConfidence { get; set; }
    public List<string> HeuristicReasons { get; set; } = new();

    public double ProjectRootConfidence { get; set; }
    public List<string> ProjectRootReasons { get; set; } = new();
    public double ProjectContainerConfidence { get; set; }
    public List<string> ProjectContainerReasons { get; set; } = new();
    public double TemplatePackConfidence { get; set; }
    public List<string> TemplatePackReasons { get; set; } = new();
    public double CameraDumpConfidence { get; set; }
    public List<string> CameraDumpReasons { get; set; } = new();
    public double CacheOnlyConfidence { get; set; }
    public List<string> CacheOnlyReasons { get; set; } = new();
    public double UnknownConfidence { get; set; }
    public List<string> UnknownReasons { get; set; } = new();

    public string? LikelyProjectRootPath { get; set; }
    public string? LikelyProjectContainerPath { get; set; }
    public string? FolderFingerprint { get; set; }

    public long SizeBytes { get; set; }
    public long FileCount { get; set; }
    public long DirCount { get; set; }
    public long PrprojCount { get; set; }
    public long AepCount { get; set; }
    public long VideoCount { get; set; }
    public long AudioCount { get; set; }
    public long ImageCount { get; set; }
    public long ImmediatePrprojCount { get; set; }
    public long ImmediateAepCount { get; set; }
    public long ImmediateVideoCount { get; set; }
    public long ImmediateAudioCount { get; set; }
    public long ImmediateImageCount { get; set; }
    public long ImmediateRawVideoCount { get; set; }
    public int ImmediateAutoSaveFolderCount { get; set; }
    public int ImmediateAudioPreviewFolderCount { get; set; }
    public int ImmediateVideoPreviewFolderCount { get; set; }

    public MarkerFile? Marker { get; set; }
}

public sealed class MarkerFile
{
    public string? Kind { get; set; }
    public string? ConfirmedBy { get; set; }
    public string? ConfirmedAt { get; set; }
    public string? LegacyClient { get; set; }
    public string? LegacyProjectName { get; set; }
    public string? Notes { get; set; }
    public string? Status { get; set; }
    public List<string>? Tags { get; set; }
}

public sealed class DuplicateCandidate
{
    public string DupType { get; set; } = string.Empty;
    public string MatchBasis { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public int Count { get; set; }
    public long SizeBytes { get; set; }
    public DateTime? LastWriteLocal { get; set; }
    public List<DuplicatePathEntry> Paths { get; set; } = new();
}

public sealed class DuplicatePathEntry
{
    public string Path { get; set; } = string.Empty;
    public DateTime? LastWriteLocal { get; set; }
}

public sealed class CacheFolderEntry
{
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class ScanError
{
    public string Path { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
}

