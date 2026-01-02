using MGF.Contracts.Abstractions.ProjectBootstrap;
using MGF.Storage.ProjectBootstrap;
using Xunit;

namespace MGF.Worker.Tests;

public sealed class ProjectStorageRootHelperTests
{
    [Fact]
    public void TestModeUsesTestRunRootKeyAndRelpath()
    {
        var rootPath = @"C:\dev\root";
        var targetPath = @"C:\dev\root\99_TestRuns\MGF25-BOOT_Client_Project";

        var rootKey = ProjectStorageRootHelper.GetRootKey(testMode: true);
        var success = ProjectStorageRootHelper.TryBuildFolderRelpath(rootPath, targetPath, out var relpath, out var error);

        Assert.True(success, error);
        Assert.Equal(ProjectStorageRootHelper.RootKeyTestRun, rootKey);
        Assert.Equal(@"99_TestRuns\MGF25-BOOT_Client_Project", relpath);
    }

    [Theory]
    [InlineData("blocked_non_real")]
    [InlineData("blocked_status_not_ready")]
    public void BlockedRootStateDoesNotUpsert(string rootState)
    {
        var summary = new ProvisioningSummary(
            Mode: "verify",
            TemplateKey: "test",
            TargetRoot: @"C:\root\path",
            ManifestPath: @"C:\root\manifest.json",
            Success: true,
            MissingRequired: Array.Empty<string>(),
            Errors: Array.Empty<string>(),
            Warnings: Array.Empty<string>()
        );

        var shouldUpsert = ProjectStorageRootHelper.ShouldUpsert(rootState, summary);
        Assert.False(shouldUpsert);
    }
}
