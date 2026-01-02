using MGF.Api.Middleware;
using MGF.Api.Services;
using MGF.Api.Square;
using MGF.Data;
using MGF.Data.Configuration;
using MGF.Data.Data;
using MGF.UseCases.Integrations.Square.IngestWebhook;
using MGF.UseCases.Projects.CreateProject;

var builder = WebApplication.CreateBuilder(args);

var mgfEnv = DatabaseConnection.GetEnvironment();
var mgfDbMode = DatabaseConnection.GetDatabaseMode();
Console.WriteLine($"MGF.Api: MGF_ENV={mgfEnv}");
Console.WriteLine($"MGF.Api: MGF_DB_MODE={mgfDbMode}");

builder.Configuration.Sources.Clear();
builder.Configuration.AddMgfConfiguration(builder.Environment.EnvironmentName, typeof(AppDbContext).Assembly);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();

builder.Services.AddScoped<ClientsService>();
builder.Services.AddScoped<PeopleService>();
builder.Services.AddScoped<JobsService>();
builder.Services.AddScoped<ProjectsService>();
builder.Services.AddScoped<ICreateProjectUseCase, CreateProjectUseCase>();
builder.Services.AddScoped<IIngestSquareWebhookUseCase, IngestSquareWebhookUseCase>();
builder.Services.AddSingleton<ISquareWebhookVerifier, SquareWebhookVerifier>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.Run();

