using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MGF.Data.Data;
using MGF.Data.Configuration;

namespace MGF.Data.IntegrationTests;

internal static class TestDb
{
    public static string ResolveConnectionString()
    {
        var config = BuildConfiguration();
        return DatabaseConnection.ResolveConnectionString(config);
    }

    public static AppDbContext CreateContext()
    {
        var connectionString = ResolveConnectionString();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;

        return new AppDbContext(options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        return new ConfigurationBuilder()
            .AddMgfConfiguration(environmentName, typeof(AppDbContext).Assembly)
            .Build();
    }
}


