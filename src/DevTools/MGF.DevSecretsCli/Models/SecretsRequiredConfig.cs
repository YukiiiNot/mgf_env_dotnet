namespace MGF.DevSecretsCli;

using System.Text.Json;

internal sealed class SecretsRequiredConfig
{
    public int SchemaVersion { get; init; } = 2;
    public List<string> RequiredKeys { get; init; } = new();
    public List<string> OptionalKeys { get; init; } = new();
    public GlobalPolicy GlobalPolicy { get; init; } = new();

    public static SecretsRequiredConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"secrets.required.json not found at {path}");
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<SecretsRequiredConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (config is null || config.RequiredKeys.Count == 0)
        {
            throw new InvalidOperationException("secrets.required.json is missing required keys.");
        }

        return config;
    }
}

internal sealed class GlobalPolicy
{
    public List<string> AllowedDbConnectionKeyPatterns { get; init; } = new();
    public List<string> DisallowedKeyPatterns { get; init; } = new();
}

