using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.DevConsole.Desktop.Hosting;
using MGF.Desktop.Views.Shells;

namespace MGF.DevConsole.Desktop;

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
                CompositionRoot.ConfigureServices(context, services);
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await Host.StartAsync();
        try
        {
            var gate = Host.Services.GetRequiredService<StartupGate>();
            await gate.EnsureEnvironmentMatchesAsync(CancellationToken.None);
        }
        catch (StartupGateException ex)
        {
            MessageBox.Show(ex.Message, "DevConsole startup blocked", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"DevConsole startup failed: {ex.Message}", "DevConsole startup blocked", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

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
