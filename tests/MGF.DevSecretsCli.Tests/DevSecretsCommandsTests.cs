using MGF.DevSecretsCli;

public class DevSecretsCommandsTests
{
    [Fact]
    public void OrderProjects_SortsByNameIgnoringCase()
    {
        var projects = new List<ProjectSecretsConfig>
        {
            new() { Name = "beta" },
            new() { Name = "Alpha" }
        };

        var ordered = DevSecretsCommands.OrderProjects(projects);
        var names = ordered.Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "Alpha", "beta" }, names);
    }
}

