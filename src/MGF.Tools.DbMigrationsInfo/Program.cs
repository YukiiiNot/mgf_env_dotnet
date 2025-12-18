using Npgsql;

namespace MGF.Tools.DbMigrationsInfo;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var parsed = ParseArgs(args);
            if (parsed.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            var connectionString = parsed.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("Missing connection string.");
                PrintUsage();
                return 2;
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await using var setReadOnly = connection.CreateCommand();
            setReadOnly.Transaction = transaction;
            setReadOnly.CommandText = "SET TRANSACTION READ ONLY;";
            await setReadOnly.ExecuteNonQueryAsync();

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                select "MigrationId", "ProductVersion"
                from "__EFMigrationsHistory"
                order by "MigrationId";
                """;

            await using var reader = await command.ExecuteReaderAsync();
            var rows = 0;
            while (await reader.ReadAsync())
            {
                var migrationId = reader.GetString(0);
                var productVersion = reader.GetString(1);
                Console.WriteLine($"{migrationId}\t{productVersion}");
                rows++;
            }

            await transaction.CommitAsync();

            if (rows == 0)
            {
                Console.Error.WriteLine("No rows found in __EFMigrationsHistory.");
            }

            return 0;
        }
        catch (PostgresException ex) when (string.Equals(ex.SqlState, "42P01", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Table __EFMigrationsHistory not found. Has the database been migrated yet?");
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to query __EFMigrationsHistory.");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private sealed record ParsedArgs(string? ConnectionString, bool ShowHelp);

    private static ParsedArgs ParseArgs(string[] args)
    {
        string? connectionString = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--connection", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    return new ParsedArgs(null, ShowHelp: false);
                }

                connectionString = args[i + 1];
                continue;
            }

            if (string.Equals(args[i], "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(args[i], "-h", StringComparison.OrdinalIgnoreCase))
            {
                return new ParsedArgs(null, ShowHelp: true);
            }
        }

        connectionString ??= Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        return new ParsedArgs(connectionString, ShowHelp: false);
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            MGF.Tools.DbMigrationsInfo

            Usage:
              dotnet run --project src/MGF.Tools.DbMigrationsInfo -- --connection "<Npgsql connection string>"

            Or set:
              DB_CONNECTION_STRING="<Npgsql connection string>"
            """
        );
    }
}
