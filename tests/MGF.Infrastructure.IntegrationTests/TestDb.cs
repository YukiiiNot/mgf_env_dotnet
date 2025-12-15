using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MGF.Infrastructure.Data;
using MGF.Infrastructure.Configuration;

namespace MGF.Infrastructure.IntegrationTests;

internal static class TestDb
{
    public static AppDbContext CreateContext()
    {
        var config = BuildConfiguration();
        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;

        return new AppDbContext(options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config");

        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        return new ConfigurationBuilder()
            .SetBasePath(configDir)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddUserSecrets(typeof(AppDbContext).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string FindRepoRoot()
    {
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
