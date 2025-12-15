using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using MGF.Infrastructure.Configuration;

namespace MGF.Infrastructure.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config");

        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        // Deterministic config loading for `dotnet ef`:
        // - Resolve repo root by walking up to MGF.sln (from CWD and fallback to AppContext.BaseDirectory)
        // - Load configuration from <repoRoot>/config regardless of where EF is invoked from
        // - Respect standard precedence: appsettings.json -> appsettings.{env}.json -> user-secrets -> env vars
        var config = new ConfigurationBuilder()
            .SetBasePath(configDir)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddUserSecretsIfAvailable(typeof(AppDbContextFactory).Assembly)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string FindRepoRoot()
    {
        // `dotnet ef` can invoke design-time services from a variety of working directories (repo root, startup project,
        // bin output, etc). To be robust, search upwards from both the current directory and AppContext.BaseDirectory.
        return TryFindRepoRootFrom(Directory.GetCurrentDirectory())
            ?? TryFindRepoRootFrom(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Could not locate repo root (MGF.sln). Run from within the repo.");
    }

    private static string? TryFindRepoRootFrom(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MGF.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}

internal static class ConfigurationBuilderUserSecretsExtensions
{
    public static IConfigurationBuilder AddUserSecretsIfAvailable(this IConfigurationBuilder builder, Assembly assembly)
    {
        // Only wire user-secrets if the project has a UserSecretsId (set via `dotnet user-secrets init`).
        if (assembly.GetCustomAttribute<UserSecretsIdAttribute>() is null)
        {
            return builder;
        }

        return builder.AddUserSecrets(assembly, optional: true);
    }
}
