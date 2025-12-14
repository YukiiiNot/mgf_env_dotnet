// See https://aka.ms/new-console-template for more information
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.Infrastructure;
using MGF.Infrastructure.Data;

namespace MGF.Tools.Migrator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("MGF.Tools.Migrator: starting migration runner...");

        try
        {
            using var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();

                    var repoRoot = FindRepoRoot();
                    var env = context.HostingEnvironment.EnvironmentName;

                    config.AddJsonFile(Path.Combine(repoRoot, "config", "appsettings.json"), optional: false, reloadOnChange: false);
                    config.AddJsonFile(Path.Combine(repoRoot, "config", $"appsettings.{env}.json"), optional: true, reloadOnChange: false);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddInfrastructure(context.Configuration);
                })
                .Build();

            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Console.WriteLine("MGF.Tools.Migrator: applying EF Core migrations...");
            await db.Database.MigrateAsync();

            Console.WriteLine("MGF.Tools.Migrator: migrations applied successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("MGF.Tools.Migrator: migration failed.");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MGF.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root (MGF.sln). Run from within the repo.");
    }
}
