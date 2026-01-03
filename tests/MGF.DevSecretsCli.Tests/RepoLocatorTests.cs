using MGF.DevSecretsCli;

public class RepoLocatorTests
{
    [Fact]
    public void FindRepoRoot_UsesMgfSln()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "a", "b");

        try
        {
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(root, "MGF.sln"), string.Empty);

            var found = RepoLocator.FindRepoRoot(nested);

            Assert.Equal(Path.GetFullPath(root), found, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void FindRepoRoot_UsesGitDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "x", "y");

        try
        {
            Directory.CreateDirectory(nested);
            Directory.CreateDirectory(Path.Combine(root, ".git"));

            var found = RepoLocator.FindRepoRoot(nested);

            Assert.Equal(Path.GetFullPath(root), found, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}

