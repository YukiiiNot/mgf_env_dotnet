using MGF.Hosting.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureAppConfiguration((context, config) =>
{
    MgfHostConfiguration.ConfigureMgfConfiguration(context, config);
});
var app = builder.Build();

app.Run();
