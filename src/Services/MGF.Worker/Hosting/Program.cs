using MGF.Data;
using MGF.Data.Configuration;
using MGF.Data.Data;
using Microsoft.Extensions.Logging;
using MGF.Contracts.Abstractions.Dropbox;
using MGF.Contracts.Abstractions.Email;
using MGF.Contracts.Abstractions.ProjectArchive;
using MGF.Contracts.Abstractions.ProjectDelivery;
using MGF.Contracts.Abstractions.ProjectBootstrap;
using MGF.Contracts.Abstractions.RootIntegrity;
using MGF.Email.Composition;
using MGF.Integrations.Dropbox;
using MGF.Integrations.Email.Preview;
using MGF.Integrations.Email.Gmail;
using MGF.Integrations.Email.Smtp;
using MGF.Integrations.Square;
using MGF.Worker.Adapters.Storage.ProjectArchive;
using MGF.Worker.Adapters.Storage.ProjectDelivery;
using MGF.Worker.Adapters.Storage.ProjectBootstrap;
using MGF.Storage.RootIntegrity;
using MGF.UseCases.DeliveryEmail.SendDeliveryEmail;
using MGF.UseCases.Operations.ProjectArchive.RunProjectArchive;
using MGF.UseCases.Operations.ProjectDelivery.RunProjectDelivery;
using MGF.UseCases.Operations.ProjectBootstrap.BootstrapProject;
using MGF.UseCases.Operations.RootIntegrity.RunRootIntegrity;
using MGF.Worker;
using MGF.Hosting.Configuration;

var mgfEnv = DatabaseConnection.GetEnvironment();
var mgfDbMode = DatabaseConnection.GetDatabaseMode();
Console.WriteLine($"MGF.Worker: MGF_ENV={mgfEnv}");
Console.WriteLine($"MGF.Worker: MGF_DB_MODE={mgfDbMode}");

var remainingArgs = ParseWorkerArgs(args, out var workerSettings);
using var host = Host.CreateDefaultBuilder(remainingArgs)
    .ConfigureAppConfiguration((context, config) =>
    {
        MgfHostConfiguration.ConfigureMgfConfiguration(context, config);
        if (workerSettings.Count > 0)
        {
            config.AddInMemoryCollection(workerSettings);
        }
    })
    .ConfigureServices((context, services) =>
    {
        services.AddInfrastructure(context.Configuration);
        services.AddTransient<GmailApiEmailSender>();
        services.AddTransient<SmtpEmailSender>();
        services.AddTransient<PreviewEmailSender>();
        services.AddTransient<IEmailSender>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var gmailSender = provider.GetRequiredService<GmailApiEmailSender>();
            var smtpSender = provider.GetRequiredService<SmtpEmailSender>();
            var previewSender = provider.GetRequiredService<PreviewEmailSender>();
            return EmailSenderFactory.Create(configuration, gmailSender, smtpSender, previewSender);
        });
        services.AddTransient<IDropboxShareLinkClient>(provider =>
        {
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient();
            return new DropboxShareLinkClient(httpClient, provider.GetRequiredService<IConfiguration>());
        });
        services.AddTransient<IDropboxFilesClient>(provider =>
        {
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient();
            return new DropboxFilesClient(httpClient, provider.GetRequiredService<IConfiguration>());
        });
        services.AddTransient<IDropboxAccessTokenProvider>(provider =>
        {
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = provider.GetService<ILogger<DropboxAccessTokenProvider>>();
            return new DropboxAccessTokenProvider(httpClient, provider.GetRequiredService<IConfiguration>(), logger);
        });
        services.AddScoped<IWorkerEmailGateway, WorkerEmailGateway>();
        services.AddScoped<ISendDeliveryEmailUseCase, SendDeliveryEmailUseCase>();
        services.AddScoped<IProjectArchiveExecutor, ProjectArchiveExecutor>();
        services.AddScoped<IRunProjectArchiveUseCase, RunProjectArchiveUseCase>();
        services.AddScoped<IProjectDeliveryExecutor, ProjectDeliveryExecutor>();
        services.AddScoped<IRunProjectDeliveryUseCase, RunProjectDeliveryUseCase>();
        services.AddScoped<IProjectBootstrapProvisioningGateway, ProjectBootstrapProvisioningGateway>();
        services.AddScoped<IBootstrapProjectUseCase, BootstrapProjectUseCase>();
        services.AddScoped<IRootIntegrityExecutor, RootIntegrityChecker>();
        services.AddScoped<IRunRootIntegrityUseCase, RunRootIntegrityUseCase>();
        services.AddHttpClient<SquareApiClient>();
        services.AddHostedService<JobWorker>();
    })
    .Build();

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

