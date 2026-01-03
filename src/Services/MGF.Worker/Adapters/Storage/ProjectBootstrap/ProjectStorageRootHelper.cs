namespace MGF.Worker.Adapters.Storage.ProjectBootstrap;

using MGF.Contracts.Abstractions.ProjectBootstrap;

public static class ProjectStorageRootHelper
{
    public const string RootKeyTestRun = "test_run";
    public const string RootKeyProjectContainer = "project_container";

    public static string GetRootKey(bool testMode)
    {
        return testMode ? RootKeyTestRun : RootKeyProjectContainer;
    }

    public static bool ShouldUpsert(string rootState, ProvisioningSummary? containerSummary)
    {
        if (containerSummary?.Success != true)
        {
            return false;
        }

        if (rootState.StartsWith("blocked_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rootState.EndsWith("_failed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static bool TryBuildFolderRelpath(
        string rootPath,
        string targetPath,
        out string folderRelpath,
        out string error)
    {
        folderRelpath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(targetPath))
        {
            error = "Root path or target path is empty.";
            return false;
        }

        var normalizedRoot = NormalizePath(rootPath);
        var normalizedTarget = NormalizePath(targetPath);

        if (!IsPathUnderRoot(normalizedRoot, normalizedTarget))
        {
            error = $"Target path is not under root. root={normalizedRoot} target={normalizedTarget}";
            return false;
        }

        var relpath = Path.GetRelativePath(normalizedRoot, normalizedTarget);
        if (string.IsNullOrWhiteSpace(relpath))
        {
            error = "Relative path could not be computed.";
            return false;
        }

        if (Path.IsPathRooted(relpath))
        {
            error = $"Relative path is rooted: {relpath}";
            return false;
        }

        if (relpath.StartsWith("..", StringComparison.Ordinal)
            || relpath.Contains("..\\", StringComparison.Ordinal)
            || relpath.Contains("../", StringComparison.Ordinal))
        {
            error = $"Relative path contains parent traversal: {relpath}";
            return false;
        }

        if (relpath.StartsWith("\\\\", StringComparison.Ordinal) || relpath.StartsWith("/", StringComparison.Ordinal))
        {
            error = $"Relative path starts with a root prefix: {relpath}";
            return false;
        }

        folderRelpath = relpath;
        return true;
    }

    private static string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsPathUnderRoot(string rootPath, string targetPath)
    {
        if (targetPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = rootPath + Path.DirectorySeparatorChar;
        return targetPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
