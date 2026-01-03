namespace MGF.DevSecretsCli;

internal static class RepoLocator
{
    public static string FindRepoRoot()
        => FindRepoRoot(Directory.GetCurrentDirectory(), AppContext.BaseDirectory);

    internal static string FindRepoRoot(params string?[] startPaths)
    {
        foreach (var start in startPaths)
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "MGF.sln")))
                {
                    return current.FullName;
                }

                if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate repo root (MGF.sln or .git) from {Directory.GetCurrentDirectory()}. " +
            "Run from the repo root or pass --required with an explicit path.");
    }
}

