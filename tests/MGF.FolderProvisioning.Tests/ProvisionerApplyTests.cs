namespace MGF.FolderProvisioning.Tests;

public sealed class ProvisionerApplyTests
{
    [Fact]
    public async Task Apply_IsIdempotent()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempBase);

        try
        {
            var template = TestTemplates.CreateTemplate(
                new FolderNode { Name = "00_Admin" },
                new FolderNode { Name = "01_PreProduction" }
            );

            var tokens = ProvisioningTokens.Create("MGF25-0001", "TestProject", null, Array.Empty<string>());
            var planner = new FolderTemplatePlanner();
            var plan = planner.Plan(template, tokens, tempBase);

            var executor = new FolderPlanExecutor(new LocalFileStore());
            var result1 = await executor.ApplyAsync(plan, seedsPath: tempBase, tokens, allowSeedOverwrite: false, CancellationToken.None);
            var result2 = await executor.ApplyAsync(plan, seedsPath: tempBase, tokens, allowSeedOverwrite: false, CancellationToken.None);

            Assert.NotEmpty(result1.CreatedItems);
            Assert.Empty(result2.CreatedItems);
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Verify_FailsWhenRequiredMissing()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempBase);

        try
        {
            var template = TestTemplates.CreateTemplate(
                new FolderNode { Name = "00_Admin" }
            );

            var tokens = ProvisioningTokens.Create("MGF25-0001", "TestProject", null, Array.Empty<string>());
            var planner = new FolderTemplatePlanner();
            var plan = planner.Plan(template, tokens, tempBase);

            var executor = new FolderPlanExecutor(new LocalFileStore());
            var verify = await executor.VerifyAsync(plan, CancellationToken.None);

            Assert.Contains("00_Admin", verify.MissingRequired);
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, recursive: true);
            }
        }
    }
}


