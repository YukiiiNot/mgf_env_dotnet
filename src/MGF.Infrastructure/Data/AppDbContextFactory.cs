using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MGF.Infrastructure.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config");
        var baseConfigPath = Path.Combine(configDir, "appsettings.json");
        var devConfigPath = Path.Combine(configDir, "appsettings.Development.json");

        var connectionString =
            Environment.GetEnvironmentVariable("Database__ConnectionString")
            ?? TryReadDatabaseConnectionString(baseConfigPath)
            ?? TryReadDatabaseConnectionString(devConfigPath);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string not found. Set `Database:ConnectionString` in `config/appsettings.Development.json` "
                + "or set environment variable `Database__ConnectionString`."
            );
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string FindRepoRoot()
    {
        // We want: <repoRoot>/config/appsettings.json
        // Search upwards from the current directory until we find MGF.sln.
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var sln = Path.Combine(dir.FullName, "MGF.sln");
            if (File.Exists(sln))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root (MGF.sln). Run from within the repo.");
    }

    private static string? TryReadDatabaseConnectionString(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("Database", out var database))
        {
            return null;
        }
        if (!database.TryGetProperty("ConnectionString", out var cs))
        {
            return null;
        }
        return cs.ValueKind == JsonValueKind.String ? cs.GetString() : null;
    }
}

