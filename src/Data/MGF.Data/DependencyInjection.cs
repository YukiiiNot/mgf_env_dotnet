using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MGF.Contracts.Abstractions;
using MGF.Data.Abstractions;
using MGF.Data.Configuration;
using MGF.Data.Data;
using MGF.Data.Data.Repositories;
using MGF.Data.Options;
using MGF.Data.Stores.Delivery;
using MGF.Data.Stores.Jobs;
using MGF.Data.Stores.Counters;
using MGF.Data.Stores.ProjectBootstrap;

namespace MGF.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<DatabaseOptions>(config.GetSection("Database"));
        services.Configure<StorageRootsOptions>(config.GetSection("Storage"));

        var connectionString = DatabaseConnection.ResolveConnectionString(config);

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IProjectRepository, EfProjectRepository>();
        services.AddScoped<IClientRepository, EfClientRepository>();
        services.AddScoped<IPersonRepository, EfPersonRepository>();
        services.AddScoped<IDeliveryEmailData, DeliveryEmailData>();
        services.AddScoped<ISquareWebhookStore, SquareWebhookStore>();
        services.AddScoped<IJobQueueStore, JobQueueStore>();
        services.AddScoped<ICounterAllocator, CounterAllocator>();
        services.AddScoped<IProjectDeliveryStore, ProjectDeliveryStore>();
        services.AddScoped<IProjectBootstrapStore, ProjectBootstrapStore>();

        return services;
    }
}



