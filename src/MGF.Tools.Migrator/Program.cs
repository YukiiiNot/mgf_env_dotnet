// See https://aka.ms/new-console-template for more information
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.Domain.Entities;
using MGF.Infrastructure;
using MGF.Infrastructure.Data;

namespace MGF.Tools.Migrator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var smoke = args.Any(a => string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase));
        var hostArgs = args.Where(a => !string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase)).ToArray();

        Console.WriteLine("MGF.Tools.Migrator: starting migration runner...");

        try
        {
            using var host = Host.CreateDefaultBuilder(hostArgs)
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

            if (smoke)
            {
                Console.WriteLine("MGF.Tools.Migrator: running smoke checks...");
                await RunSmokeAsync(db);
                Console.WriteLine("MGF.Tools.Migrator: smoke checks passed.");
            }
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

    private static async Task RunSmokeAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            var cliId = $"cli_smoke_{Guid.NewGuid():N}";
            var perId = $"per_smoke_{Guid.NewGuid():N}";
            var prjId = $"prj_smoke_{Guid.NewGuid():N}";

            db.Add(new Client(cliId, "Smoke Client"));
            db.Add(new Person(perId, "SM"));

            db.Add(
                new Project(
                    prjId: prjId,
                    projectCode: $"SMOKE_{Guid.NewGuid():N}",
                    cliId: cliId,
                    name: "Smoke Project",
                    statusKey: "active",
                    phaseKey: "planning",
                    priorityKey: "normal",
                    typeKey: "video_edit",
                    pathsRootKey: "local",
                    folderRelpath: "smoke/project"
                )
            );

            db.Add(new ProjectMember(prjId, perId, "producer"));

            await db.SaveChangesAsync();

            if (await db.Clients.CountAsync() < 1)
            {
                throw new InvalidOperationException("Smoke failed: expected at least 1 client row.");
            }

            if (await db.People.CountAsync() < 1)
            {
                throw new InvalidOperationException("Smoke failed: expected at least 1 person row.");
            }

            if (await db.Projects.CountAsync() < 1)
            {
                throw new InvalidOperationException("Smoke failed: expected at least 1 project row.");
            }

            if (await db.ProjectMembers.CountAsync() < 1)
            {
                throw new InvalidOperationException("Smoke failed: expected at least 1 project_member row.");
            }

            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
