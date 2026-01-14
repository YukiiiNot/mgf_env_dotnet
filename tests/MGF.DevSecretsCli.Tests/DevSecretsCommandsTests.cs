using System.Text.Json;
using MGF.DevSecretsCli;

public class DevSecretsCommandsTests
{
    private static readonly SemaphoreSlim RepoLock = new(1, 1);

    [Fact]
    public async Task ImportAsync_DoesNotOverwriteExistingKeysUnlessForced()
    {
        await RepoLock.WaitAsync();
        var originalDirectory = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(Path.Combine(tempRoot, "MGF.sln"), string.Empty);

            var configDir = Path.Combine(tempRoot, "config");
            Directory.CreateDirectory(configDir);

            var configPath = Path.Combine(configDir, "appsettings.Development.json");
            File.WriteAllText(configPath, "{\"Security\":{\"ApiKey\":\"existing\"}}");

            var requiredPath = Path.Combine(tempRoot, "required.json");
            File.WriteAllText(requiredPath, """{"schemaVersion":2,"requiredKeys":["Security:ApiKey"],"optionalKeys":[],"globalPolicy":{}}""");

            var exportPath = Path.Combine(tempRoot, "dev-secrets.export.json");
            WriteExport(exportPath, "Security:ApiKey", "new");

            Directory.SetCurrentDirectory(tempRoot);

            var exitCode = await DevSecretsCommands.ImportAsync(
                exportPath,
                requiredPath,
                dryRun: false,
                verbose: false,
                force: false,
                cancellationToken: CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Equal("existing", ReadApiKey(configPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            try
            {
                Directory.Delete(tempRoot, true);
            }
            catch
            {
                // Ignore cleanup failures for temp dirs.
            }

            RepoLock.Release();
        }
    }

    [Fact]
    public async Task ImportAsync_OverwritesExistingKeysWhenForced()
    {
        await RepoLock.WaitAsync();
        var originalDirectory = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(Path.Combine(tempRoot, "MGF.sln"), string.Empty);

            var configDir = Path.Combine(tempRoot, "config");
            Directory.CreateDirectory(configDir);

            var configPath = Path.Combine(configDir, "appsettings.Development.json");
            File.WriteAllText(configPath, "{\"Security\":{\"ApiKey\":\"existing\"}}");

            var requiredPath = Path.Combine(tempRoot, "required.json");
            File.WriteAllText(requiredPath, """{"schemaVersion":2,"requiredKeys":["Security:ApiKey"],"optionalKeys":[],"globalPolicy":{}}""");

            var exportPath = Path.Combine(tempRoot, "dev-secrets.export.json");
            WriteExport(exportPath, "Security:ApiKey", "forced");

            Directory.SetCurrentDirectory(tempRoot);

            var exitCode = await DevSecretsCommands.ImportAsync(
                exportPath,
                requiredPath,
                dryRun: false,
                verbose: false,
                force: true,
                cancellationToken: CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Equal("forced", ReadApiKey(configPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            try
            {
                Directory.Delete(tempRoot, true);
            }
            catch
            {
                // Ignore cleanup failures for temp dirs.
            }

            RepoLock.Release();
        }
    }

    private static void WriteExport(string path, string key, string value)
    {
        var export = new SecretsExportFile
        {
            SchemaVersion = 2,
            ToolVersion = "test",
            ExportedAtUtc = "2026-01-01T00:00:00Z",
            Secrets = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                [key] = value
            }
        };

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string? ReadApiKey(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.TryGetProperty("Security", out var security)
            && security.TryGetProperty("ApiKey", out var apiKey)
            && apiKey.ValueKind == JsonValueKind.String)
        {
            return apiKey.GetString();
        }

        return null;
    }
}
