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
        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        return new ConfigurationBuilder()
            .AddMgfConfiguration(environmentName, typeof(AppDbContext).Assembly)
            .Build();
    }
}
