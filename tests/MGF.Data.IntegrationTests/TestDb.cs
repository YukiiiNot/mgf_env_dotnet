using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.Data.Data;
using MGF.Data.Configuration;
using MGF.Hosting.Configuration;

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
        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                MgfHostConfiguration.ConfigureMgfConfiguration(context, config);
            })
            .Build();

        return (IConfigurationRoot)host.Services.GetRequiredService<IConfiguration>();
    }
}


