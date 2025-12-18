namespace MGF.Tools.SquareImport.Commands;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.Infrastructure;
using MGF.Infrastructure.Configuration;
using MGF.Infrastructure.Data;
using MGF.Tools.SquareImport.Reporting;
using Npgsql;

internal static class SquareImportCommandRunner
{
    public static async Task<int> RunAsync(
        string commandName,
        bool dryRun,
        Func<AppDbContext, CancellationToken, Task<ImportSummary>> action,
        CancellationToken cancellationToken
    )
    {
        Console.WriteLine($"square-import {commandName}: starting (dry-run={dryRun})");

        try
        {
            var mgfEnv = DatabaseConnection.GetEnvironment();
            var mgfDbMode = DatabaseConnection.GetDatabaseMode();
            Console.WriteLine($"square-import {commandName}: MGF_ENV={mgfEnv}");
            Console.WriteLine($"square-import {commandName}: MGF_DB_MODE={mgfDbMode}");

            using var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();

                    var env = context.HostingEnvironment.EnvironmentName;
                    config.AddMgfConfiguration(env, typeof(AppDbContext).Assembly);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddInfrastructure(context.Configuration);
                })
                .Build();

            var configuration = host.Services.GetRequiredService<IConfiguration>();
            var connectionString = DatabaseConnection.ResolveConnectionString(configuration);
            LogConnectionInfo(connectionString);

            await PreflightAsync(connectionString, cancellationToken);

            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var summary = await action(db, cancellationToken);
            summary.WriteToConsole(commandName);

            return summary.Errors > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"square-import {commandName}: failed");
            Console.Error.WriteLine(ex.ToString());
            new ImportSummary(Inserted: 0, Updated: 0, Skipped: 0, Errors: 1).WriteToConsole(commandName);
            return 1;
        }
    }

    private static void LogConnectionInfo(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Console.WriteLine(
            $"square-import: DB target Host={builder.Host};Port={builder.Port};Database={builder.Database};Username={builder.Username}"
        );

        Console.WriteLine(
            $"square-import: DB options SslMode={builder.SslMode};Pooling={builder.Pooling};Multiplexing={builder.Multiplexing}"
        );
    }

    private static async Task PreflightAsync(string connectionString, CancellationToken cancellationToken)
    {
        Console.WriteLine("square-import: DB preflight...");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            _ = await cmd.ExecuteScalarAsync(cancellationToken);
            Console.WriteLine("square-import: DB preflight ok.");
        }
        finally
        {
            await conn.CloseAsync();
        }
    }
}
