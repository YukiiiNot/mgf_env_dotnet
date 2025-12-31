namespace MGF.Provisioning.Tests;

public sealed class PlannerValidationTests
{
    [Fact]
    public void Planner_RejectsMgfOutsideAdmin()
    {
        var template = TestTemplates.CreateTemplate(
            new FolderNode
            {
                Name = "01_PreProduction",
                Children = new List<FolderNode>
                {
                    new FolderNode { Name = ".mgf" }
                }
            }
        );

        var planner = new FolderTemplatePlanner();
        var tokens = ProvisioningTokens.Create("MGF25-0001", "Test", null, Array.Empty<string>());

        var ex = Assert.Throws<InvalidOperationException>(() => planner.Plan(template, tokens, Path.GetTempPath()));
        Assert.Contains(".mgf", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Planner_RequiresTopLevelNumericPrefix()
    {
        var template = TestTemplates.CreateTemplate(
            new FolderNode { Name = "Admin" }
        );

        var planner = new FolderTemplatePlanner();
        var tokens = ProvisioningTokens.Create("MGF25-0001", "Test", null, Array.Empty<string>());

        var ex = Assert.Throws<InvalidOperationException>(() => planner.Plan(template, tokens, Path.GetTempPath()));
        Assert.Contains("Top-level", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

