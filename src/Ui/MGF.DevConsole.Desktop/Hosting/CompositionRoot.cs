using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.DevConsole.Desktop.Api;
using MGF.DevConsole.Desktop.Modules.Status.ViewModels;
using MGF.DevConsole.Desktop.Modules.Status.Views;
using MGF.Desktop.Views.Shells;

namespace MGF.DevConsole.Desktop.Hosting;

public static class CompositionRoot
{
    public static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddHttpClient<MetaApiClient>(httpClient =>
        {
            var baseUrl = context.Configuration["Api:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("Api:BaseUrl is not configured. Set the API base URL before starting DevConsole.");
            }

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException($"Api:BaseUrl is not a valid absolute URL: {baseUrl}");
            }

            httpClient.BaseAddress = baseUri;
            httpClient.Timeout = TimeSpan.FromSeconds(3);

            var apiKey = context.Configuration["Security:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Security:ApiKey is not configured. Set the API key before starting DevConsole.");
            }

            httpClient.DefaultRequestHeaders.Add("X-MGF-API-KEY", apiKey);

            var operatorName = context.Configuration["Security:Operator"];
            if (!string.IsNullOrWhiteSpace(operatorName))
            {
                httpClient.DefaultRequestHeaders.Add("X-MGF-Operator", operatorName);
            }
        });

        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<StatusView>(sp =>
        {
            var view = new StatusView();
            view.DataContext = sp.GetRequiredService<StatusViewModel>();
            return view;
        });
        services.AddSingleton<MainWindow>(sp =>
        {
            var window = new MainWindow();
            var viewModel = sp.GetRequiredService<StatusViewModel>();
            window.SetMainContent(sp.GetRequiredService<StatusView>());
            viewModel.Start();
            window.Closed += (_, _) => viewModel.Stop();
            return window;
        });
        services.AddSingleton<StartupGate>();
    }
}
