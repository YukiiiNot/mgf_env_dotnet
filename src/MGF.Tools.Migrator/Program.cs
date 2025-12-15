// See https://aka.ms/new-console-template for more information
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MGF.Domain.Entities;
using MGF.Infrastructure;
using MGF.Infrastructure.Configuration;
using MGF.Infrastructure.Data;
using MGF.Infrastructure.Data.Seeding;
using Npgsql;

namespace MGF.Tools.Migrator;

public static class Program
{
    private static ILoggerFactory? NpgsqlLoggerFactory { get; set; }

    public static async Task<int> Main(string[] args)
    {
        var smoke = args.Any(a => string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase));
        var hostArgs = args.Where(a => !string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase)).ToArray();

        Console.WriteLine("MGF.Tools.Migrator: starting migration runner...");

        try
        {
            var mgfEnv = DatabaseConnection.GetEnvironment();
            var mgfDbMode = DatabaseConnection.GetDatabaseMode();
            Console.WriteLine($"MGF.Tools.Migrator: MGF_ENV={mgfEnv}");
            Console.WriteLine($"MGF.Tools.Migrator: MGF_DB_MODE={mgfDbMode}");
            TryEnableNpgsqlLogging(mgfEnv);
            LogVersionInfo();

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

                    if (mgfEnv == MgfEnvironment.Dev)
                    {
                        services.AddDbContext<AppDbContext>(options =>
                        {
                            options.EnableDetailedErrors();
                            options.EnableSensitiveDataLogging();
                        });
                    }
                })
                .Build();

            var configuration = host.Services.GetRequiredService<IConfiguration>();
            var connectionString = DatabaseConnection.ResolveConnectionString(configuration);
            LogConnectionInfo(connectionString);

            await RunWithTransientRetryAsync(
                operationName: "database connection preflight",
                action: cancellationToken => PreflightAsync(connectionString, cancellationToken),
                maxAttempts: 3,
                initialDelay: TimeSpan.FromSeconds(1)
            );

            await RunWithTransientRetryAsync(
                operationName: "EF Core migrations + seed",
                action: async cancellationToken =>
                {
                    await using var scope = host.Services.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    Console.WriteLine("MGF.Tools.Migrator: applying EF Core migrations...");
                    await db.Database.MigrateAsync(cancellationToken);
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
                },
                maxAttempts: 3,
                initialDelay: TimeSpan.FromSeconds(2)
            );

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("MGF.Tools.Migrator: migration failed.");
            Console.Error.WriteLine(ex.ToString());
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

    private static void LogVersionInfo()
    {
        var efVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "unknown";
        var npgsqlVersion = typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString() ?? "unknown";
        Console.WriteLine($"MGF.Tools.Migrator: EF Core={efVersion}; Npgsql={npgsqlVersion}");
    }

    private static void TryEnableNpgsqlLogging(MgfEnvironment mgfEnv)
    {
        if (!GetEnvBool("MGF_NPGSQL_LOGGING"))
        {
            return;
        }

        var parameterLoggingEnabled = GetEnvBool("MGF_NPGSQL_LOG_PARAMETERS");
        if (parameterLoggingEnabled && mgfEnv != MgfEnvironment.Dev)
        {
            Console.WriteLine("MGF.Tools.Migrator: refusing to enable Npgsql parameter logging outside Dev.");
            parameterLoggingEnabled = false;
        }

        NpgsqlLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("Npgsql", LogLevel.Debug);
        });

        NpgsqlLoggingConfiguration.InitializeLogging(NpgsqlLoggerFactory, parameterLoggingEnabled);

        Console.WriteLine(
            $"MGF.Tools.Migrator: Npgsql logging enabled (parameters={parameterLoggingEnabled})."
        );
    }

    private static bool GetEnvBool(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static void LogConnectionInfo(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Console.WriteLine(
            $"MGF.Tools.Migrator: DB target Host={builder.Host};Port={builder.Port};Database={builder.Database};Username={builder.Username}"
        );

        Console.WriteLine(
            $"MGF.Tools.Migrator: DB options SslMode={builder.SslMode};Pooling={builder.Pooling};Multiplexing={builder.Multiplexing}"
        );
    }

    private static async Task PreflightAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            _ = await cmd.ExecuteScalarAsync(cancellationToken);
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    private static async Task RunWithTransientRetryAsync(
        string operationName,
        Func<CancellationToken, Task> action,
        int maxAttempts,
        TimeSpan initialDelay
    )
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Must be >= 1.");
        }

        var delay = initialDelay;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await action(CancellationToken.None);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                Console.Error.WriteLine(
                    $"MGF.Tools.Migrator: transient failure during {operationName} (attempt {attempt}/{maxAttempts}): {ex.GetType().Name}: {ex.Message}"
                );

                Console.Error.WriteLine($"MGF.Tools.Migrator: retrying in {delay.TotalSeconds:0.0}s...");
                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
            }
        }
    }

    private static bool IsTransient(Exception ex)
    {
        return ex switch
        {
            ObjectDisposedException { ObjectName: "System.Threading.ManualResetEventSlim" } => true,
            NpgsqlException npgsqlException => npgsqlException.IsTransient,
            TimeoutException => true,
            System.IO.IOException => true,
            System.Net.Sockets.SocketException => true,
            _ when ex.InnerException is not null => IsTransient(ex.InnerException),
            _ => false,
        };
    }

    private static async Task RunSmokeAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            var clientId = $"cli_smoke_{Guid.NewGuid():N}";
            var personId = $"per_smoke_{Guid.NewGuid():N}";
            var projectId = $"prj_smoke_{Guid.NewGuid():N}";

            db.Add(new Client(clientId, "Smoke Client"));
            db.Add(new Person(personId, "Smoke", "User", initials: "SU"));

            db.Add(
                new Project(
                    projectId: projectId,
                    projectCode: $"MGF99-{Random.Shared.Next(0, 10000):0000}",
                    clientId: clientId,
                    name: "Smoke Project",
                    statusKey: "active",
                    phaseKey: "planning",
                    priorityKey: "normal"
                )
            );

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

            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
