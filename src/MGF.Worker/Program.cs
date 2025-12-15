using System.Reflection;
using Microsoft.Extensions.Configuration.UserSecrets;
using MGF.Infrastructure;
using MGF.Infrastructure.Configuration;
using MGF.Infrastructure.Data;
using MGF.Worker;

var mgfEnv = DatabaseConnection.GetEnvironment();
var mgfDbMode = DatabaseConnection.GetDatabaseMode();
Console.WriteLine($"MGF.Worker: MGF_ENV={mgfEnv}");
Console.WriteLine($"MGF.Worker: MGF_DB_MODE={mgfDbMode}");

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.Sources.Clear();

var repoRoot = FindRepoRoot();
var configDir = Path.Combine(repoRoot, "config");

builder.Configuration.SetBasePath(configDir);
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.json",
    optional: true,
    reloadOnChange: false
);
builder.Configuration.AddUserSecretsIfAvailable(typeof(AppDbContext).Assembly);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<JobWorker>();

var host = builder.Build();
host.Run();

static string FindRepoRoot()
{
    return TryFindRepoRootFrom(Directory.GetCurrentDirectory())
        ?? TryFindRepoRootFrom(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("Could not locate repo root (MGF.sln). Run from within the repo.");
}

static string? TryFindRepoRootFrom(string startPath)
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

static class ConfigurationBuilderUserSecretsExtensions
{
    public static IConfigurationBuilder AddUserSecretsIfAvailable(this IConfigurationBuilder builder, Assembly assembly)
    {
        if (assembly.GetCustomAttribute<UserSecretsIdAttribute>() is null)
        {
            return builder;
        }

        return builder.AddUserSecrets(assembly, optional: true);
    }
}
