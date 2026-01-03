namespace MGF.LegacyAuditCli.Commands;

internal static class Guardrails
{
    private const string AllowDestructiveEnv = "MGF_ALLOW_DESTRUCTIVE";
    private const string DestructiveAckEnv = "MGF_DESTRUCTIVE_ACK";
    private const string DestructiveAckValue = "I_UNDERSTAND";

    public static bool EnsureApply(bool apply, out string error)
    {
        if (apply)
        {
            error = string.Empty;
            return true;
        }

        error = "This command writes reports. Re-run with --apply to proceed.";
        return false;
    }

    public static bool EnsureDestructiveAck(out string error)
    {
        var allow = Environment.GetEnvironmentVariable(AllowDestructiveEnv);
        var ack = Environment.GetEnvironmentVariable(DestructiveAckEnv);
        if (string.Equals(allow, "true", StringComparison.OrdinalIgnoreCase)
            && string.Equals(ack, DestructiveAckValue, StringComparison.Ordinal))
        {
            error = string.Empty;
            return true;
        }

        error = $"Destructive actions require {AllowDestructiveEnv}=true and {DestructiveAckEnv}={DestructiveAckValue}.";
        return false;
    }

    public static bool TryFindRepoRoot(out string repoRoot, out string error)
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        for (var current = start; current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                repoRoot = current.FullName;
                error = string.Empty;
                return true;
            }
        }

        repoRoot = string.Empty;
        error = "Repo root not found (missing .git). Run from within the repo.";
        return false;
    }

    public static string GetRuntimeRoot(string repoRoot)
    {
        return Path.Combine(repoRoot, "runtime");
    }

    public static string GetDefaultScanOutputPath(string repoRoot, string rootPath, DateTimeOffset? now = null)
    {
        var stamp = (now ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd_HHmm");
        var label = SanitizeLabel(rootPath);
        return Path.Combine(GetRuntimeRoot(repoRoot), "legacy_audit", "outputs", $"{label}_{stamp}");
    }

    public static string GetDefaultExportOutputPath(string repoRoot, string reportPath, DateTimeOffset? now = null)
    {
        var stamp = (now ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd_HHmm");
        var label = SanitizeLabel(Path.GetFileName(Path.GetDirectoryName(reportPath) ?? string.Empty));
        if (string.IsNullOrWhiteSpace(label))
        {
            label = "export";
        }

        return Path.Combine(GetRuntimeRoot(repoRoot), "legacy_audit", "exports", $"{label}_{stamp}");
    }

    public static bool TryResolveOutputPath(
        string repoRoot,
        string? requestedPath,
        string defaultPath,
        out string fullPath,
        out string error)
    {
        var chosen = string.IsNullOrWhiteSpace(requestedPath) ? defaultPath : requestedPath;
        var resolved = Path.GetFullPath(chosen);
        var runtimeRoot = Path.GetFullPath(GetRuntimeRoot(repoRoot));

        if (!IsUnderRoot(resolved, runtimeRoot))
        {
            fullPath = string.Empty;
            error = $"Output path must be under {runtimeRoot}.";
            return false;
        }

        fullPath = resolved;
        error = string.Empty;
        return true;
    }

    private static bool IsUnderRoot(string path, string root)
    {
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeLabel(string value)
    {
        var trimmed = value.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.StartsWith("\\\\", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.TrimStart('\\');
        }

        var label = trimmed.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
        foreach (var ch in Path.GetInvalidFileNameChars())
        {
            label = label.Replace(ch, '_');
        }

        return label.Replace(' ', '_');
    }
}

