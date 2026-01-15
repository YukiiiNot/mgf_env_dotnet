using MGF.DevSecretsCli;

public class SecretsFilterTests
{
    [Fact]
    public void Filter_AllowsRequiredAndOptional_AndBlocksDisallowed()
    {
        var source = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Database:Dev:DirectConnectionString"] = "dev-conn",
            ["Database:Prod:DirectConnectionString"] = "prod-conn",
            ["Integrations:Dropbox:AccessToken"] = "token",
        };

        var required = new SecretsRequiredConfig
        {
            RequiredKeys = new() { "Database:Dev:DirectConnectionString" },
            OptionalKeys = new() { "Database:Prod:DirectConnectionString", "Integrations:Dropbox:AccessToken" },
            GlobalPolicy = new GlobalPolicy
            {
                AllowedDbConnectionKeyPatterns = new() { "*Dev*" },
                DisallowedKeyPatterns = new() { "*Prod*" }
            }
        };

        var result = SecretsFilter.Filter(source, required);

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

        var required = new SecretsRequiredConfig
        {
            RequiredKeys = new() { "Database:Dev:DirectConnectionString" },
            GlobalPolicy = new GlobalPolicy
            {
                AllowedDbConnectionKeyPatterns = new() { "*Dev*" },
                DisallowedKeyPatterns = new() { "*Dev*" }
            }
        };

        var result = SecretsFilter.Filter(source, required);

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

        var required = new SecretsRequiredConfig
        {
            RequiredKeys = new() { "B:Key", "A:Key" }
        };

        var result = SecretsFilter.Filter(source, required);
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
        var export = new SecretsExportFile
        {
            Secrets = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["Database:Dev:DirectConnectionString"] = "dev",
                ["Unexpected:Key"] = "value"
            }
        };

        var required = new SecretsRequiredConfig
        {
            RequiredKeys = new() { "Database:Dev:DirectConnectionString" }
        };

        var validation = SecretsFilter.ValidateExport(export, required);
        Assert.False(validation.IsValid);
        Assert.Contains("not allowed", validation.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateExport_FailsOnDisallowedKey()
    {
        var export = new SecretsExportFile
        {
            Secrets = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["Database:Dev:DirectConnectionString"] = "dev",
                ["Database:Prod:DirectConnectionString"] = "prod"
            }
        };

        var required = new SecretsRequiredConfig
        {
            RequiredKeys = new() { "Database:Dev:DirectConnectionString" },
            OptionalKeys = new() { "Database:Prod:DirectConnectionString" },
            GlobalPolicy = new GlobalPolicy
            {
                AllowedDbConnectionKeyPatterns = new() { "*Dev*" },
                DisallowedKeyPatterns = new() { "*Prod*" }
            }
        };

        var validation = SecretsFilter.ValidateExport(export, required);
        Assert.False(validation.IsValid);
        Assert.Contains("violates policy", validation.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
