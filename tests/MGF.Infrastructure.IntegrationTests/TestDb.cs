using Microsoft.EntityFrameworkCore;
using MGF.Infrastructure.Data;

namespace MGF.Infrastructure.IntegrationTests;

internal static class TestDb
{
    public static string GetConnectionStringOrThrow()
    {
        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Integration tests require a Postgres connection string. Set env var `Database__ConnectionString` (Npgsql format)."
            );
        }

        return connectionString;
    }

    public static AppDbContext CreateContext()
    {
        var connectionString = GetConnectionStringOrThrow();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;

        return new AppDbContext(options);
    }
}

