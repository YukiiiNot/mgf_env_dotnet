using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MGF.Hosting.Configuration;

public static class MgfHostConfiguration
{
    private const int MaxRepoSearchDepth = 6;

    public static void ConfigureMgfConfiguration(HostBuilderContext context, IConfigurationBuilder config)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        config.Sources.Clear();

        var environmentName = context.HostingEnvironment.EnvironmentName;
        var configDir = ResolveConfigDirectory();
        var loadedJsonFiles = new List<string>();
        string? configDirUsed = null;

        if (!string.IsNullOrWhiteSpace(configDir))
        {
            configDirUsed = configDir;
            var rootAppSettings = Path.Combine(configDir, "appsettings.json");
            if (!File.Exists(rootAppSettings))
            {
                throw new FileNotFoundException($"Required config file not found: {rootAppSettings}");
            }

            config.AddJsonFile(rootAppSettings, optional: false, reloadOnChange: false);
            loadedJsonFiles.Add(rootAppSettings);
            var envAppSettings = Path.Combine(configDir, $"appsettings.{environmentName}.json");
            config.AddJsonFile(
                envAppSettings,
                optional: true,
                reloadOnChange: false);
            if (File.Exists(envAppSettings))
            {
                loadedJsonFiles.Add(envAppSettings);
            }
        }
        else
        {
            var currentDir = Directory.GetCurrentDirectory();
            if (TryAddRequiredJson(config, currentDir, environmentName, loadedJsonFiles))
            {
                configDirUsed = currentDir;
            }
            else
            {
                var baseDir = AppContext.BaseDirectory;
                if (TryAddRequiredJson(config, baseDir, environmentName, loadedJsonFiles))
                {
                    configDirUsed = baseDir;
                }
                else
                {
                    configDirUsed = currentDir;
                    var rootAppSettings = Path.Combine(currentDir, "appsettings.json");
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    loadedJsonFiles.Add(rootAppSettings);
                    var envAppSettings = Path.Combine(currentDir, $"appsettings.{environmentName}.json");
                    config.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);
                    if (File.Exists(envAppSettings))
                    {
                        loadedJsonFiles.Add(envAppSettings);
                    }
                }
            }
        }

        config.AddEnvironmentVariables();
        TryLogDevelopmentDiagnostics(context, configDirUsed, loadedJsonFiles, config);
    }

    private static string? ResolveConfigDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("MGF_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var fullPath = Path.GetFullPath(configured);
            if (!Directory.Exists(fullPath))
            {
                throw new InvalidOperationException($"MGF_CONFIG_DIR is set but directory does not exist: {fullPath}");
            }

            return fullPath;
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var depth = 0; depth <= MaxRepoSearchDepth && current is not null; depth++)
        {
            var candidate = Path.Combine(current.FullName, "config", "appsettings.json");
            if (File.Exists(candidate))
            {
                return Path.Combine(current.FullName, "config");
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool TryAddRequiredJson(
        IConfigurationBuilder config,
        string baseDir,
        string environmentName,
        List<string> loadedJsonFiles)
    {
        var appSettingsPath = Path.Combine(baseDir, "appsettings.json");
        if (!File.Exists(appSettingsPath))
        {
            return false;
        }

        config.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false);
        loadedJsonFiles.Add(appSettingsPath);
        var envAppSettings = Path.Combine(baseDir, $"appsettings.{environmentName}.json");
        config.AddJsonFile(
            envAppSettings,
            optional: true,
            reloadOnChange: false);
        if (File.Exists(envAppSettings))
        {
            loadedJsonFiles.Add(envAppSettings);
        }
        return true;
    }

    private static void TryLogDevelopmentDiagnostics(
        HostBuilderContext context,
        string? configDir,
        IReadOnlyList<string> loadedJsonFiles,
        IConfigurationBuilder config)
    {
        if (!context.HostingEnvironment.IsDevelopment())
        {
            return;
        }

        try
        {
            var resolvedDir = string.IsNullOrWhiteSpace(configDir)
                ? Directory.GetCurrentDirectory()
                : configDir;

            Console.WriteLine($"MGF.Config: configDir={resolvedDir}");

            if (loadedJsonFiles.Count == 0)
            {
                Console.WriteLine("MGF.Config: loadedJsonFiles=none");
            }
            else
            {
                foreach (var file in loadedJsonFiles)
                {
                    Console.WriteLine($"MGF.Config: loaded {Path.GetFileName(file)}");
                }
            }

            var built = config.Build();
            var requiredKeys = TryLoadRequiredKeys(configDir);
            if (requiredKeys.Count > 0)
            {
                var missing = requiredKeys
                    .Where(key => string.IsNullOrWhiteSpace(built[key]))
                    .ToArray();

                if (missing.Length > 0)
                {
                    Console.WriteLine($"MGF.Config: missing required keys: {string.Join(", ", missing)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MGF.Config: dev config diagnostics failed: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> TryLoadRequiredKeys(string? configDir)
    {
        if (string.IsNullOrWhiteSpace(configDir))
        {
            return Array.Empty<string>();
        }

        var repoRoot = Directory.GetParent(configDir)?.FullName;
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            return Array.Empty<string>();
        }

        var requiredPath = Path.Combine(repoRoot, "tools", "dev-secrets", "secrets.required.json");
        if (!File.Exists(requiredPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(requiredPath));
            if (!doc.RootElement.TryGetProperty("requiredKeys", out var required)
                || required.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var keys = new List<string>();
            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        keys.Add(value);
                    }
                }
            }

            return keys;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
