using System;
using System.IO;
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

        if (!string.IsNullOrWhiteSpace(configDir))
        {
            var rootAppSettings = Path.Combine(configDir, "appsettings.json");
            if (!File.Exists(rootAppSettings))
            {
                throw new FileNotFoundException($"Required config file not found: {rootAppSettings}");
            }

            config.AddJsonFile(rootAppSettings, optional: false, reloadOnChange: false);
            config.AddJsonFile(
                Path.Combine(configDir, $"appsettings.{environmentName}.json"),
                optional: true,
                reloadOnChange: false);
        }
        else
        {
            var currentDir = Directory.GetCurrentDirectory();
            if (!TryAddRequiredJson(config, currentDir, environmentName))
            {
                var baseDir = AppContext.BaseDirectory;
                if (!TryAddRequiredJson(config, baseDir, environmentName))
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    config.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);
                }
            }
        }

        config.AddEnvironmentVariables();
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

    private static bool TryAddRequiredJson(IConfigurationBuilder config, string baseDir, string environmentName)
    {
        var appSettingsPath = Path.Combine(baseDir, "appsettings.json");
        if (!File.Exists(appSettingsPath))
        {
            return false;
        }

        config.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false);
        config.AddJsonFile(
            Path.Combine(baseDir, $"appsettings.{environmentName}.json"),
            optional: true,
            reloadOnChange: false);
        return true;
    }
}
