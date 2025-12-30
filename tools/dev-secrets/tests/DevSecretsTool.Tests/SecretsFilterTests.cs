using MGF.Tools.DevSecrets;

public class SecretsFilterTests
{
    [Fact]
    public void ParseListOutput_ExtractsKeysAndValues()
    {
        var output = """
            Secrets for project: sample
              Database:Dev:DirectConnectionString = Host=localhost
              Integrations:Dropbox:AccessToken = token123
            """;

        var parsed = DotnetUserSecrets.ParseListOutput(output);

        Assert.Equal("Host=localhost", parsed["Database:Dev:DirectConnectionString"]);
        Assert.Equal("token123", parsed["Integrations:Dropbox:AccessToken"]);
    }

    [Fact]
    public void Filter_AllowsRequiredAndOptional_AndBlocksDisallowed()
    {
        var source = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Database:Dev:DirectConnectionString"] = "dev-conn",
            ["Database:Prod:DirectConnectionString"] = "prod-conn",
            ["Integrations:Dropbox:AccessToken"] = "token",
        };

        var project = new ProjectSecretsConfig
        {
            Name = "Test",
            RequiredKeys = new() { "Database:Dev:DirectConnectionString" },
            OptionalKeys = new() { "Database:Prod:DirectConnectionString", "Integrations:Dropbox:AccessToken" }
        };

        var policy = new GlobalPolicy
        {
            AllowedDbConnectionKeyPatterns = new() { "*Dev*" },
            DisallowedKeyPatterns = new() { "*Prod*" }
        };

        var result = SecretsFilter.Filter(source, project, policy);

        Assert.True(result.Allowed.ContainsKey("Database:Dev:DirectConnectionString"));
        Assert.True(result.Allowed.ContainsKey("Integrations:Dropbox:AccessToken"));
        Assert.False(result.Allowed.ContainsKey("Database:Prod:DirectConnectionString"));
        Assert.Empty(result.MissingRequired);
    }

    [Fact]
    public void Filter_RequiredDisallowedCountsAsMissing()
    {
        var source = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Database:Dev:DirectConnectionString"] = "dev-conn"
        };

        var project = new ProjectSecretsConfig
        {
            Name = "Test",
            RequiredKeys = new() { "Database:Dev:DirectConnectionString" }
        };

        var policy = new GlobalPolicy
        {
            AllowedDbConnectionKeyPatterns = new() { "*Dev*" },
            DisallowedKeyPatterns = new() { "*Dev*" }
        };

        var result = SecretsFilter.Filter(source, project, policy);

        Assert.Contains("Database:Dev:DirectConnectionString", result.MissingRequired);
    }

    [Fact]
    public void Filter_ProducesDeterministicOrdering()
    {
        var source = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["B:Key"] = "b",
            ["A:Key"] = "a",
        };

        var project = new ProjectSecretsConfig
        {
            Name = "Test",
            RequiredKeys = new() { "B:Key", "A:Key" }
        };

        var policy = new GlobalPolicy
        {
            AllowedDbConnectionKeyPatterns = new(),
            DisallowedKeyPatterns = new()
        };

        var result = SecretsFilter.Filter(source, project, policy);
        var keys = result.Allowed.Keys.ToArray();

        Assert.Equal(new[] { "A:Key", "B:Key" }, keys);
    }

    [Fact]
    public void Policy_RejectsNonDevDatabaseKeys()
    {
        var policy = new GlobalPolicy
        {
            AllowedDbConnectionKeyPatterns = new() { "*Dev*" },
            DisallowedKeyPatterns = new()
        };

        Assert.False(SecretsPolicy.IsAllowedKey("Database:Prod:DirectConnectionString", policy));
    }

    [Fact]
    public void ValidateExport_RejectsKeyNotInRequiredJson()
    {
        var export = new ProjectSecretsExport
        {
            Name = "Test",
            UserSecretsId = "id",
            Secrets = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["Database:Dev:DirectConnectionString"] = "dev",
                ["Unexpected:Key"] = "value"
            }
        };

        var project = new ProjectSecretsConfig
        {
            Name = "Test",
            UserSecretsId = "id",
            RequiredKeys = new() { "Database:Dev:DirectConnectionString" }
        };

        var policy = new GlobalPolicy
        {
            AllowedDbConnectionKeyPatterns = new() { "*Dev*" },
            DisallowedKeyPatterns = new()
        };

        var validation = SecretsFilter.ValidateExport(export, project, policy);
        Assert.False(validation.IsValid);
        Assert.Contains("not allowed", validation.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateExport_FailsOnDisallowedKey()
    {
        var export = new ProjectSecretsExport
        {
            Name = "Test",
            UserSecretsId = "id",
            Secrets = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["Database:Dev:DirectConnectionString"] = "dev",
                ["Database:Prod:DirectConnectionString"] = "prod"
            }
        };

        var project = new ProjectSecretsConfig
        {
            Name = "Test",
            UserSecretsId = "id",
            RequiredKeys = new() { "Database:Dev:DirectConnectionString" },
            OptionalKeys = new() { "Database:Prod:DirectConnectionString" }
        };

        var policy = new GlobalPolicy
        {
            AllowedDbConnectionKeyPatterns = new() { "*Dev*" },
            DisallowedKeyPatterns = new() { "*Prod*" }
        };

        var validation = SecretsFilter.ValidateExport(export, project, policy);
        Assert.False(validation.IsValid);
        Assert.Contains("violates policy", validation.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
