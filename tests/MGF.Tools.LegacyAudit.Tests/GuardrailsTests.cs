using MGF.Tools.LegacyAudit.Commands;

namespace MGF.Tools.LegacyAudit.Tests;

public sealed class GuardrailsTests
{
    [Fact]
    public void EnsureApplyRequiresExplicitFlag()
    {
        var allowed = Guardrails.EnsureApply(false, out var error);

        Assert.False(allowed);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void OutputPathMustStayUnderRuntimeRoot()
    {
        var repoRoot = CreateTempRepoRoot();
        try
        {
            var defaultOutput = Path.Combine(repoRoot, "runtime", "legacy_audit", "outputs", "run");
            var requested = Path.Combine(repoRoot, "..", "outside");

            var allowed = Guardrails.TryResolveOutputPath(repoRoot, requested, defaultOutput, out _, out var error);

            Assert.False(allowed);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
        finally
        {
            SafeDelete(repoRoot);
        }
    }

    private static string CreateTempRepoRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MGF.LegacyAudit.Guardrails", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        return root;
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp dirs.
        }
    }
}
