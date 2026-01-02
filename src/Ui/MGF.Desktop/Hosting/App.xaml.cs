using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.Data.Options;
using MGF.Desktop.Views.Shells;

namespace MGF.Desktop;

public partial class App : System.Windows.Application
{
    public IHost Host { get; }

    public App()
    {
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.Sources.Clear();
                var env = context.HostingEnvironment;
                config.AddJsonFile(Path.Combine("config", "appsettings.json"), optional: false, reloadOnChange: true);
                config.AddJsonFile(Path.Combine("config", $"appsettings.{env.EnvironmentName}.json"), optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<StorageRootsOptions>(context.Configuration.GetSection("Storage"));
                services.Configure<DatabaseOptions>(context.Configuration.GetSection("Database"));
                services.Configure<FeatureFlagsOptions>(context.Configuration.GetSection("FeatureFlags"));

                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await Host.StartAsync();
        var window = Host.Services.GetRequiredService<MainWindow>();
        window.Show();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await Host.StopAsync();
        Host.Dispose();
        base.OnExit(e);
    }
}
