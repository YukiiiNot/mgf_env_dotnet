using MGF.Data;
using MGF.Data.Configuration;
using MGF.Data.Data;
using Microsoft.Extensions.Logging;
using MGF.Contracts.Abstractions.Dropbox;
using MGF.Contracts.Abstractions.Email;
using MGF.Integrations.Dropbox;
using MGF.Integrations.Email.Gmail;
using MGF.Integrations.Email.Smtp;
using MGF.UseCases.DeliveryEmail.SendDeliveryEmail;
using MGF.UseCases.ProjectBootstrap.BootstrapProject;
using MGF.Worker;
using MGF.Worker.Email;
using MGF.Worker.Email.Sending;
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
builder.Services.AddTransient<GmailApiEmailSender>();
builder.Services.AddTransient<SmtpEmailSender>();
builder.Services.AddTransient<IEmailSender>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var gmailSender = services.GetRequiredService<GmailApiEmailSender>();
    var smtpSender = services.GetRequiredService<SmtpEmailSender>();
    return EmailSenderFactory.Create(configuration, gmailSender, smtpSender);
});
builder.Services.AddTransient<IDropboxShareLinkClient>(services =>
{
    var httpClient = services.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new DropboxShareLinkClient(httpClient, services.GetRequiredService<IConfiguration>());
});
builder.Services.AddTransient<IDropboxFilesClient>(services =>
{
    var httpClient = services.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new DropboxFilesClient(httpClient, services.GetRequiredService<IConfiguration>());
});
builder.Services.AddTransient<IDropboxAccessTokenProvider>(services =>
{
    var httpClient = services.GetRequiredService<IHttpClientFactory>().CreateClient();
    var logger = services.GetService<ILogger<DropboxAccessTokenProvider>>();
    return new DropboxAccessTokenProvider(httpClient, services.GetRequiredService<IConfiguration>(), logger);
});
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

