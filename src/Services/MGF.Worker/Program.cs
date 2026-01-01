using MGF.Data;
using MGF.Data.Configuration;
using MGF.Data.Data;
using MGF.UseCases.DeliveryEmail.SendDeliveryEmail;
using MGF.UseCases.ProjectBootstrap.BootstrapProject;
using MGF.Worker;
using MGF.Worker.Email;
using MGF.Worker.ProjectBootstrap;
using MGF.Worker.Square;

var mgfEnv = DatabaseConnection.GetEnvironment();
var mgfDbMode = DatabaseConnection.GetDatabaseMode();
Console.WriteLine($"MGF.Worker: MGF_ENV={mgfEnv}");
Console.WriteLine($"MGF.Worker: MGF_DB_MODE={mgfDbMode}");

var remainingArgs = ParseWorkerArgs(args, out var workerSettings);
var builder = Host.CreateApplicationBuilder(remainingArgs);

builder.Configuration.Sources.Clear();
builder.Configuration.AddMgfConfiguration(builder.Environment.EnvironmentName, typeof(AppDbContext).Assembly);
if (workerSettings.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(workerSettings);
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IWorkerEmailGateway, WorkerEmailGateway>();
builder.Services.AddScoped<ISendDeliveryEmailUseCase, SendDeliveryEmailUseCase>();
builder.Services.AddScoped<IProjectBootstrapProvisioningGateway, ProjectBootstrapProvisioningGateway>();
builder.Services.AddScoped<IBootstrapProjectUseCase, BootstrapProjectUseCase>();
builder.Services.AddHttpClient<SquareApiClient>();
builder.Services.AddHostedService<JobWorker>();

var host = builder.Build();
host.Run();

static string[] ParseWorkerArgs(string[] args, out Dictionary<string, string?> settings)
{
    settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    var remaining = new List<string>();

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (string.Equals(arg, "--once", StringComparison.OrdinalIgnoreCase))
        {
            settings["Worker:MaxJobs"] = "1";
            settings["Worker:ExitWhenIdle"] = "true";
            continue;
        }

        if (string.Equals(arg, "--maxJobs", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var maxJobs))
            {
                settings["Worker:MaxJobs"] = maxJobs.ToString();
                i++;
                continue;
            }

            throw new ArgumentException("Missing or invalid value for --maxJobs.");
        }

        remaining.Add(arg);
    }

    return remaining.ToArray();
}

