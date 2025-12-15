// See https://aka.ms/new-console-template for more information
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
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
                    var configDir = Path.Combine(repoRoot, "config");

                    config.SetBasePath(configDir);
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                    config.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);
                    config.AddUserSecretsIfAvailable(typeof(AppDbContext).Assembly);
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

            Console.WriteLine("MGF.Tools.Migrator: seeding lookup tables...");
            await LookupSeeder.SeedAsync(db);
            Console.WriteLine("MGF.Tools.Migrator: seeding completed.");
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

    private static IConfigurationBuilder AddUserSecretsIfAvailable(this IConfigurationBuilder builder, Assembly assembly)
    {
        // Only wire user-secrets if the MGF.Infrastructure project has a UserSecretsId (recommended for local dev).
        if (assembly.GetCustomAttribute<UserSecretsIdAttribute>() is null)
        {
            return builder;
        }

        return builder.AddUserSecrets(assembly, optional: true);
    }
}
