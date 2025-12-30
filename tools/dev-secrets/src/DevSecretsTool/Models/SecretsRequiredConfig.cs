namespace MGF.Tools.DevSecrets;

using System.Text.Json;

internal sealed class SecretsRequiredConfig
{
    public List<ProjectSecretsConfig> Projects { get; init; } = new();
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

        if (config is null || config.Projects.Count == 0)
        {
            throw new InvalidOperationException("secrets.required.json is missing required project entries.");
        }

        return config;
    }

    public ProjectSecretsConfig? FindByUserSecretsId(string userSecretsId)
        => Projects.FirstOrDefault(project =>
            string.Equals(project.UserSecretsId, userSecretsId, StringComparison.OrdinalIgnoreCase));
}

internal sealed class ProjectSecretsConfig
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string UserSecretsId { get; init; } = string.Empty;
    public List<string> RequiredKeys { get; init; } = new();
    public List<string> OptionalKeys { get; init; } = new();
}

internal sealed class GlobalPolicy
{
    public List<string> AllowedDbConnectionKeyPatterns { get; init; } = new();
    public List<string> DisallowedKeyPatterns { get; init; } = new();
}
