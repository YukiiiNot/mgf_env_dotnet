using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MGF.Contracts.Abstractions;
using MGF.Contracts.Abstractions.Integrations.Square;
using MGF.Contracts.Abstractions.ProjectBootstrap;
using MGF.Contracts.Abstractions.Operations.Jobs;
using MGF.Contracts.Abstractions.Operations.People;
using MGF.Contracts.Abstractions.Operations.Projects;
using MGF.Contracts.Abstractions.Operations.StorageRoots;
using MGF.Contracts.Abstractions.ProjectArchive;
using MGF.Contracts.Abstractions.ProjectDelivery;
using MGF.Contracts.Abstractions.Projects;
using MGF.Contracts.Abstractions.RootIntegrity;
using MGF.Data.Configuration;
using MGF.Data.Data;
using MGF.Data.Data.Repositories;
using MGF.Data.Options;
using MGF.Data.Stores.Delivery;
using MGF.Data.Stores.Jobs;
using MGF.Data.Stores.Counters;
using MGF.Data.Stores.Operations;
using MGF.Data.Stores.Projects;
using MGF.Data.Stores.ProjectArchive;
using MGF.Data.Stores.ProjectBootstrap;
using MGF.Data.Stores.RootIntegrity;

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
        services.AddScoped<IProjectDeliveryData, ProjectDeliveryData>();
        services.AddScoped<IProjectArchiveData, ProjectArchiveData>();
        services.AddScoped<ISquareWebhookStore, SquareWebhookStore>();
        services.AddScoped<IJobQueueStore, JobQueueStore>();
        services.AddScoped<ICounterAllocator, CounterAllocator>();
        services.AddScoped<IProjectDeliveryStore, ProjectDeliveryStore>();
        services.AddScoped<IProjectArchiveStore, ProjectArchiveStore>();
        services.AddScoped<IProjectBootstrapStore, ProjectBootstrapStore>();
        services.AddScoped<IProjectCreationStore, ProjectCreationStore>();
        services.AddScoped<IJobOpsStore, JobOpsStore>();
        services.AddScoped<IPeopleOpsStore, PeopleOpsStore>();
        services.AddScoped<IProjectOpsStore, ProjectOpsStore>();
        services.AddScoped<IProjectContactOpsStore, ProjectContactOpsStore>();
        services.AddScoped<IStorageRootContractStore, StorageRootContractStore>();
        services.AddScoped<IRootIntegrityStore, RootIntegrityStore>();

        return services;
    }
}



