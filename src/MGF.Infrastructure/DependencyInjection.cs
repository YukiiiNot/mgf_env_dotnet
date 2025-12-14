using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MGF.Application.Abstractions;
using MGF.Infrastructure.Data;
using MGF.Infrastructure.Data.Repositories;
using MGF.Infrastructure.Options;

namespace MGF.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<DatabaseOptions>(config.GetSection("Database"));

        var connectionString = config["Database:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database:ConnectionString is not set. Set it in `config/appsettings.Development.json` "
                + "or via environment variable `Database__ConnectionString`."
            );
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IProjectRepository, EfProjectRepository>();
        services.AddScoped<IClientRepository, EfClientRepository>();
        services.AddScoped<IPersonRepository, EfPersonRepository>();

        return services;
    }
}

