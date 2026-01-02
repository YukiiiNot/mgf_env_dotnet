using MGF.Worker.Adapters.Storage.ProjectBootstrap;

public sealed class ProjectBootstrapGuardsTests
{
    [Fact]
    public void TryValidateStart_RejectsNonReadyStatus()
    {
        var allowed = ProjectBootstrapGuards.TryValidateStart("active", force: false, out var error, out var alreadyProvisioning);

        Assert.False(allowed);
        Assert.False(alreadyProvisioning);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateStart_DetectsAlreadyProvisioning()
    {
        var allowed = ProjectBootstrapGuards.TryValidateStart("provisioning", force: false, out var error, out var alreadyProvisioning);

        Assert.False(allowed);
        Assert.True(alreadyProvisioning);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateTestCleanup_BlocksWhenTargetExistsWithoutAllow()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var testPath = Path.Combine(root, "99_TestRuns", "TEST");

        Directory.CreateDirectory(testPath);
        try
        {
            var allowed = ProjectBootstrapGuards.TryValidateTestCleanup(root, testPath, allowTestCleanup: false, out var error);
            Assert.False(allowed);
            Assert.NotNull(error);
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
