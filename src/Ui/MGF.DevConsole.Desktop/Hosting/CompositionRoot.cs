using System;
using System.Net.Http;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.DevConsole.Desktop.Api;
using MGF.DevConsole.Desktop.Hosting.Connection;
using MGF.DevConsole.Desktop.Modules.Jobs.ViewModels;
using MGF.DevConsole.Desktop.Modules.Jobs.Views;
using MGF.DevConsole.Desktop.Modules.Projects.ViewModels;
using MGF.DevConsole.Desktop.Modules.Projects.Views;
using MGF.DevConsole.Desktop.Modules.Status.ViewModels;
using MGF.DevConsole.Desktop.Modules.Status.Views;
using MGF.Desktop.Views.Shells;

namespace MGF.DevConsole.Desktop.Hosting;

public static class CompositionRoot
{
    public static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddHttpClient<MetaApiClient>(httpClient => ConfigureApiHttpClient(context, httpClient));
        services.AddSingleton<IMetaApiClient>(sp => sp.GetRequiredService<MetaApiClient>());
        services.AddHttpClient<JobsApiClient>(httpClient => ConfigureApiHttpClient(context, httpClient));
        services.AddSingleton<IJobsApiClient>(sp => sp.GetRequiredService<JobsApiClient>());
        services.AddHttpClient<ProjectsApiClient>(httpClient => ConfigureApiHttpClient(context, httpClient));
        services.AddSingleton<IProjectsApiClient>(sp => sp.GetRequiredService<ProjectsApiClient>());

        services.AddSingleton<ApiConnectionStateStore>();
        services.AddSingleton<IApiConnectionStateStore>(sp => sp.GetRequiredService<ApiConnectionStateStore>());
        services.AddSingleton<IApiConnectionProbe, ApiConnectionProbe>();
        services.AddSingleton<IApiConnectionMonitor, ApiConnectionMonitor>();

        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<StatusView>(sp =>
        {
            var view = new StatusView();
            view.DataContext = sp.GetRequiredService<StatusViewModel>();
            return view;
        });
        services.AddSingleton<JobsViewModel>();
        services.AddSingleton<JobsView>(sp =>
        {
            var view = new JobsView();
            view.DataContext = sp.GetRequiredService<JobsViewModel>();
            return view;
        });
        services.AddSingleton<ProjectsViewModel>();
        services.AddSingleton<ProjectsView>(sp =>
        {
            var view = new ProjectsView();
            view.DataContext = sp.GetRequiredService<ProjectsViewModel>();
            return view;
        });
        services.AddSingleton<MainWindow>(sp =>
        {
            var window = new MainWindow();
            var statusViewModel = sp.GetRequiredService<StatusViewModel>();
            var jobsViewModel = sp.GetRequiredService<JobsViewModel>();
            var projectsViewModel = sp.GetRequiredService<ProjectsViewModel>();
            var connectionMonitor = sp.GetRequiredService<IApiConnectionMonitor>();
            window.SetStatusContent(sp.GetRequiredService<StatusView>());
            var tabControl = new TabControl();
            tabControl.Items.Add(new TabItem
            {
                Header = "Jobs",
                Content = sp.GetRequiredService<JobsView>()
            });
            tabControl.Items.Add(new TabItem
            {
                Header = "Projects",
                Content = sp.GetRequiredService<ProjectsView>()
            });
            window.SetMainContent(tabControl);
            window.Loaded += (_, _) =>
            {
                connectionMonitor.Start();
                statusViewModel.Start();
                jobsViewModel.Start();
                projectsViewModel.Start();
            };
            window.Closed += (_, _) =>
            {
                statusViewModel.Stop();
                jobsViewModel.Stop();
                projectsViewModel.Stop();
                connectionMonitor.Stop();
            };
            return window;
        });
        services.AddSingleton<StartupGate>();
    }

    private static void ConfigureApiHttpClient(HostBuilderContext context, HttpClient httpClient)
    {
        var baseUrl = context.Configuration["Api:BaseUrl"];
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            httpClient.BaseAddress = baseUri;
        }
        httpClient.Timeout = TimeSpan.FromSeconds(3);

        var apiKey = context.Configuration["Security:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            httpClient.DefaultRequestHeaders.Add("X-MGF-API-KEY", apiKey);
        }

        var operatorName = context.Configuration["Security:Operator"];
        if (!string.IsNullOrWhiteSpace(operatorName))
        {
            httpClient.DefaultRequestHeaders.Add("X-MGF-Operator", operatorName);
        }
    }
}
