using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.DevConsole.Desktop.Hosting;
using MGF.Desktop.Views.Shells;
using MGF.Hosting.Configuration;

namespace MGF.DevConsole.Desktop;

public partial class App : System.Windows.Application
{
    public IHost Host { get; }

    public App()
    {
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                MgfHostConfiguration.ConfigureMgfConfiguration(context, config);
            })
            .ConfigureServices((context, services) =>
            {
                CompositionRoot.ConfigureServices(context, services);
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
