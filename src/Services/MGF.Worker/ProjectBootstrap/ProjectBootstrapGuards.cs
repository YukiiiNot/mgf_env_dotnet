namespace MGF.Worker.ProjectBootstrap;

using MGF.Tools.Provisioner;

internal static class ProjectBootstrapGuards
{
    private const string StatusReady = "ready_to_provision";
    private const string StatusProvisioning = "provisioning";

    public static bool TryValidateStart(string statusKey, bool force, out string? error, out bool alreadyProvisioning)
    {
        alreadyProvisioning = false;
        error = null;

        if (force)
        {
            return true;
        }

        if (string.Equals(statusKey, StatusProvisioning, StringComparison.OrdinalIgnoreCase))
        {
            alreadyProvisioning = true;
            error = "Project is already provisioning.";
            return false;
        }

        if (!string.Equals(statusKey, StatusReady, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Project status '{statusKey}' is not ready_to_provision.";
            return false;
        }

        return true;
    }

    public static string BuildTestContainerBasePath(string rootPath)
    {
        return Path.Combine(rootPath, "99_TestRuns");
    }

    public static string BuildTestContainerTargetPath(string rootPath, ProvisioningTokens tokens)
    {
        var projectCode = tokens.ProjectCode ?? "PROJECT";
        var clientName = tokens.ClientName ?? "CLIENT";
        var projectName = tokens.ProjectName ?? "PROJECT";
        var folderName = $"{projectCode}_{clientName}_{projectName}";

        PathSafety.EnsureSafeSegment(folderName, "test run folder name");

        return Path.Combine(rootPath, "99_TestRuns", folderName);
    }

    public static bool TryValidateTestCleanup(
        string rootPath,
        string testContainerPath,
        bool allowTestCleanup,
        out string? error)
    {
        error = null;

        if (!Directory.Exists(testContainerPath))
        {
            return true;
        }

        if (!allowTestCleanup)
        {
            error = "Test target exists; set allowTestCleanup=true to delete and re-run.";
            return false;
        }

        var rootFull = NormalizePath(rootPath);
        var testFull = NormalizePath(testContainerPath);

        if (!testFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            error = "Test cleanup blocked because target is outside the configured root.";
            return false;
        }

        var relative = Path.GetRelativePath(rootFull, testFull);
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        if (!string.Equals(firstSegment, "99_TestRuns", StringComparison.OrdinalIgnoreCase))
        {
            error = "Test cleanup blocked because target is not under 99_TestRuns.";
            return false;
        }

        return true;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
