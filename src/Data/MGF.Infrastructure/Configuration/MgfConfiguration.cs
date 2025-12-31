namespace MGF.Infrastructure.Configuration;

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

public static class MgfConfiguration
{
    public static IConfigurationBuilder AddMgfConfiguration(
        this IConfigurationBuilder builder,
        string environmentName,
        Assembly userSecretsAssembly
    )
    {
        var configDir = ResolveConfigDirectory();

        if (!string.IsNullOrWhiteSpace(configDir))
        {
            builder.AddJsonFile(Path.Combine(configDir, "appsettings.json"), optional: true, reloadOnChange: false);
            builder.AddJsonFile(
                Path.Combine(configDir, $"appsettings.{environmentName}.json"),
                optional: true,
                reloadOnChange: false
            );
        }

        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        builder.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);

        var baseDir = AppContext.BaseDirectory;
        builder.AddJsonFile(Path.Combine(baseDir, "appsettings.json"), optional: true, reloadOnChange: false);
        builder.AddJsonFile(
            Path.Combine(baseDir, $"appsettings.{environmentName}.json"),
            optional: true,
            reloadOnChange: false
        );

        builder.AddUserSecretsIfAvailable(userSecretsAssembly);
        builder.AddEnvironmentVariables();

        return builder;
    }

    public static string? ResolveConfigDirectory()
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

        var fromCwd = Path.Combine(Directory.GetCurrentDirectory(), "config");
        if (Directory.Exists(fromCwd))
        {
            return fromCwd;
        }

        var fromBaseDir = Path.Combine(AppContext.BaseDirectory, "config");
        if (Directory.Exists(fromBaseDir))
        {
            return fromBaseDir;
        }

        return null;
    }

    private static IConfigurationBuilder AddUserSecretsIfAvailable(this IConfigurationBuilder builder, Assembly assembly)
    {
        if (assembly.GetCustomAttribute<UserSecretsIdAttribute>() is null)
        {
            return builder;
        }

        return builder.AddUserSecrets(assembly, optional: true);
    }
}

