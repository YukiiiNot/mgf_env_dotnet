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
