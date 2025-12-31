using MGF.Tools.LegacyAudit.Models;

namespace MGF.Tools.LegacyAudit.Scanning;

internal sealed class DirectoryAggregate
{
    public DirectoryAggregate(string path, int depth)
    {
        Path = path;
        Depth = depth;
    }

    public string Path { get; }
    public int Depth { get; }
    public DateTime? LastWriteLocal { get; set; }
    public long ImmediateSizeBytes { get; set; }
    public long ImmediateFileCount { get; set; }
    public long ImmediateDirCount { get; set; }
    public long ImmediatePrprojCount { get; set; }
    public long ImmediateAepCount { get; set; }
    public long ImmediateVideoCount { get; set; }
    public long ImmediateAudioCount { get; set; }
    public long ImmediateImageCount { get; set; }
    public long ImmediateRawVideoCount { get; set; }
    public long ImmediateZipCount { get; set; }
    public long ImmediateMogrtCount { get; set; }
    public long ImmediatePresetCount { get; set; }
    public long ImmediatePdfCount { get; set; }
    public int ImmediateAutoSaveFolderCount { get; set; }
    public int ImmediateAudioPreviewFolderCount { get; set; }
    public int ImmediateVideoPreviewFolderCount { get; set; }
    public bool ImmediateHasRendersFolder { get; set; }
    public bool ImmediateHasExportsFolder { get; set; }
    public bool ImmediateHasGraphicsFolder { get; set; }
    public bool ImmediateHasAudioFolder { get; set; }
    public bool ImmediateHasFootageFolder { get; set; }
    public bool ImmediateHasProjectFilesFolder { get; set; }

    public List<string> ChildDirs { get; } = new();
    public List<string> FingerprintEntries { get; } = new();
    public List<string> CameraFolderMatches { get; } = new();

    public bool HasAutoSaveFolder { get; set; }
    public bool HasAudioPreviewFolder { get; set; }
    public bool HasVideoPreviewFolder { get; set; }
    public bool HasRendersFolder { get; set; }
    public bool HasExportsFolder { get; set; }
    public bool HasGraphicsFolder { get; set; }
    public bool HasAudioFolder { get; set; }
    public bool HasFootageFolder { get; set; }
    public bool HasProjectFilesFolder { get; set; }
    public MarkerFile? Marker { get; set; }
    public bool HasScanError { get; set; }
    public long TotalSizeBytes { get; set; }
    public long FileCount { get; set; }
    public long DirCount { get; set; }
    public long PrprojCount { get; set; }
    public long AepCount { get; set; }
    public long VideoCount { get; set; }
    public long AudioCount { get; set; }
    public long ImageCount { get; set; }
    public long RawVideoCount { get; set; }
    public long ZipCount { get; set; }
    public long MogrtCount { get; set; }
    public long PresetCount { get; set; }
    public long PdfCount { get; set; }
    public int AutoSaveFolderCount { get; set; }
    public int AudioPreviewFolderCount { get; set; }
    public int VideoPreviewFolderCount { get; set; }
    public string? Fingerprint { get; set; }
}
