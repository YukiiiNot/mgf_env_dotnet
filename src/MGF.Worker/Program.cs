using MGF.Infrastructure;
using MGF.Infrastructure.Configuration;
using MGF.Infrastructure.Data;
using MGF.Worker;
using MGF.Worker.Square;

var mgfEnv = DatabaseConnection.GetEnvironment();
var mgfDbMode = DatabaseConnection.GetDatabaseMode();
Console.WriteLine($"MGF.Worker: MGF_ENV={mgfEnv}");
Console.WriteLine($"MGF.Worker: MGF_DB_MODE={mgfDbMode}");

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.Sources.Clear();
builder.Configuration.AddMgfConfiguration(builder.Environment.EnvironmentName, typeof(AppDbContext).Assembly);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpClient<SquareApiClient>();
builder.Services.AddHostedService<JobWorker>();

var host = builder.Build();
host.Run();
