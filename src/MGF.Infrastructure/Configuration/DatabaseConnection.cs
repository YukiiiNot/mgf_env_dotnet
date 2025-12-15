using Microsoft.Extensions.Configuration;

namespace MGF.Infrastructure.Configuration;

public enum MgfEnvironment
{
    Dev,
    Staging,
    Prod,
}

public enum MgfDatabaseMode
{
    Auto,
    Direct,
    Pooler,
}

public static class DatabaseConnection
{
    public static MgfEnvironment GetEnvironment()
    {
        var value = Environment.GetEnvironmentVariable("MGF_ENV");
        if (string.IsNullOrWhiteSpace(value))
        {
            return MgfEnvironment.Dev;
        }

        if (string.Equals(value, "Dev", StringComparison.OrdinalIgnoreCase))
        {
            return MgfEnvironment.Dev;
        }

        if (string.Equals(value, "Staging", StringComparison.OrdinalIgnoreCase))
        {
            return MgfEnvironment.Staging;
        }

        if (string.Equals(value, "Prod", StringComparison.OrdinalIgnoreCase))
        {
            return MgfEnvironment.Prod;
        }

        return MgfEnvironment.Dev;
    }

    public static MgfDatabaseMode GetDatabaseMode()
    {
        var value = Environment.GetEnvironmentVariable("MGF_DB_MODE");
        if (string.IsNullOrWhiteSpace(value))
        {
            return MgfDatabaseMode.Auto;
        }

        if (string.Equals(value, "direct", StringComparison.OrdinalIgnoreCase))
        {
            return MgfDatabaseMode.Direct;
        }

        if (string.Equals(value, "pooler", StringComparison.OrdinalIgnoreCase))
        {
            return MgfDatabaseMode.Pooler;
        }

        return MgfDatabaseMode.Auto;
    }

    public static string ResolveConnectionString(IConfiguration config)
    {
        var env = GetEnvironment();
        var envName = env.ToString();

        string? connectionString = null;

        var mode = GetDatabaseMode();
        if (mode == MgfDatabaseMode.Direct)
        {
            connectionString = config[$"Database:{envName}:DirectConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = config["Database:DirectConnectionString"];
            }
        }
        else if (mode == MgfDatabaseMode.Pooler)
        {
            connectionString = config[$"Database:{envName}:PoolerConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = config["Database:PoolerConnectionString"];
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = config[$"Database:{envName}:ConnectionString"];
        }
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = config["Database:ConnectionString"];
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(GetMissingConnectionStringMessage(envName));
        }

        return connectionString;
    }

    public static void EnsureDestructiveAllowedOrThrow(string operation)
    {
        var env = GetEnvironment();
        if (env == MgfEnvironment.Prod)
        {
            throw new InvalidOperationException(
                $"Destructive operation blocked in Prod (MGF_ENV=Prod): {operation}. Use a non-Prod database."
            );
        }

        var allow = Environment.GetEnvironmentVariable("MGF_ALLOW_DESTRUCTIVE");
        if (!string.Equals(allow, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Destructive operation requires MGF_ALLOW_DESTRUCTIVE=true (MGF_ENV={env}): {operation}."
            );
        }
    }

    private static string GetMissingConnectionStringMessage(string envName)
    {
        return
            $"Database connection string not found for MGF_ENV={envName}. "
            + $"Set user-secrets `Database:{envName}:ConnectionString` (preferred), "
            + $"or `Database:{envName}:DirectConnectionString` / `Database:{envName}:PoolerConnectionString` (when using `MGF_DB_MODE`), "
            + "or legacy `Database:ConnectionString`, "
            + $"or set env var `Database__{envName}__ConnectionString` (preferred), "
            + $"or `Database__{envName}__DirectConnectionString` / `Database__{envName}__PoolerConnectionString` (when using `MGF_DB_MODE`), "
            + "or legacy `Database__ConnectionString`.";
    }
}
