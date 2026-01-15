using MGF.Api.Middleware;
using MGF.Api.Services;
using MGF.Contracts.Abstractions.Integrations.Square;
using MGF.Integrations.Square;
using MGF.Data;
using MGF.Data.Configuration;
using MGF.Data.Data;
using MGF.Hosting.Configuration;
using MGF.UseCases.Integrations.Square.IngestWebhook;
using MGF.UseCases.Operations.People.ListPeople;
using MGF.UseCases.Projects.CreateProject;

var builder = WebApplication.CreateBuilder(args);

var mgfEnv = DatabaseConnection.GetEnvironment();
var mgfDbMode = DatabaseConnection.GetDatabaseMode();
Console.WriteLine($"MGF.Api: MGF_ENV={mgfEnv}");
Console.WriteLine($"MGF.Api: MGF_DB_MODE={mgfDbMode}");

builder.Host.ConfigureAppConfiguration((context, config) =>
{
    MgfHostConfiguration.ConfigureMgfConfiguration(context, config);
});

if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["Security:ApiKey"]))
{
    throw new InvalidOperationException(
        "MGF.Api: Security:ApiKey is not configured. " +
        "Set it in config/appsettings.Development.json (or run devsecrets import; see dev-secrets.md) " +
        "or set SECURITY__APIKEY.");
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();

builder.Services.AddScoped<ClientsService>();
builder.Services.AddScoped<PeopleService>();
builder.Services.AddScoped<JobsService>();
builder.Services.AddScoped<ProjectsService>();
builder.Services.AddScoped<MetaService>();
builder.Services.AddScoped<ICreateProjectUseCase, CreateProjectUseCase>();
builder.Services.AddScoped<IIngestSquareWebhookUseCase, IngestSquareWebhookUseCase>();
builder.Services.AddScoped<IListPeopleUseCase, ListPeopleUseCase>();
builder.Services.AddSingleton<ISquareWebhookVerifier, SquareWebhookVerifier>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.Run();

