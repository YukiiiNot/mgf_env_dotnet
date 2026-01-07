using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.Desktop.Views.Shells;

namespace MGF.DevConsole.Desktop.Hosting;

public static class CompositionRoot
{
    public static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
    }
}
