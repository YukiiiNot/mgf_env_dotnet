using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MGF.Contracts.Abstractions;
using MGF.Data.Configuration;
using MGF.Data.Data;
using MGF.Data.Data.Repositories;
using MGF.Data.Options;

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

        return services;
    }
}



