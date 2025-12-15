using Microsoft.EntityFrameworkCore;
using MGF.Infrastructure.Data;

namespace MGF.Infrastructure.IntegrationTests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private sealed record SeedStatement(string TableName, string Sql);

    // Keep in sync with `src/MGF.Tools.Migrator/LookupSeeder.cs`.
    private static readonly SeedStatement[] SeedStatements =
    [
        new(
            "project_statuses",
            """
            INSERT INTO public.project_statuses (status_key, display_name, sort_order)
            VALUES
              ('active', 'Active', 10),
              ('archived', 'Archived', 90)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "project_phases",
            """
            INSERT INTO public.project_phases (phase_key, display_name, sort_order)
            VALUES
              ('planning', 'Planning', 10),
              ('scheduling', 'Scheduling', 20),
              ('production', 'Production', 30),
              ('editing', 'Editing', 40),
              ('delivery', 'Delivery', 50)
            ON CONFLICT (phase_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "project_priorities",
            """
            INSERT INTO public.project_priorities (priority_key, display_name, sort_order)
            VALUES
              ('low', 'Low', 10),
              ('normal', 'Normal', 20),
              ('high', 'High', 30)
            ON CONFLICT (priority_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "project_types",
            """
            INSERT INTO public.project_types (type_key, display_name, sort_order)
            VALUES
              ('video_edit', 'Video Edit', 10),
              ('shoot', 'Shoot', 20),
              ('internal', 'Internal', 30)
            ON CONFLICT (type_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "project_roles",
            """
            INSERT INTO public.project_roles (role_key, display_name, sort_order)
            VALUES
              ('producer', 'Producer', 10),
              ('editor', 'Editor', 20),
              ('assistant', 'Assistant', 30)
            ON CONFLICT (role_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "project_code_counters",
            """
            INSERT INTO public.project_code_counters (year, next_seq)
            VALUES ((EXTRACT(YEAR FROM now()))::int, 1)
            ON CONFLICT (year) DO UPDATE
            SET next_seq = GREATEST(project_code_counters.next_seq, EXCLUDED.next_seq);
            """
        ),
    ];

    public async Task InitializeAsync()
    {
        await using var db = TestDb.CreateContext();
        await db.Database.MigrateAsync();

        await SeedLookupsAsync(db);
        await ResetCoreDataAsync(db);
    }

    public async Task DisposeAsync()
    {
        await using var db = TestDb.CreateContext();
        await ResetCoreDataAsync(db);
    }

    public async Task ResetAsync()
    {
        await using var db = TestDb.CreateContext();
        await ResetCoreDataAsync(db);
    }

    private static async Task ResetCoreDataAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            await db.Database.ExecuteSqlRawAsync(
                """
                TRUNCATE TABLE
                  public.project_members,
                  public.projects,
                  public.people,
                  public.clients
                CASCADE;
                """
            );

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO public.project_code_counters (year, next_seq)
                VALUES ((EXTRACT(YEAR FROM now()))::int, 1)
                ON CONFLICT (year) DO UPDATE SET next_seq = 1;
                """
            );

            await transaction.CommitAsync();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task SeedLookupsAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            var existingTables = await GetExistingPublicTablesAsync(db);

            foreach (var statement in SeedStatements)
            {
                if (!existingTables.Contains(statement.TableName))
                {
                    continue;
                }

                await db.Database.ExecuteSqlRawAsync(statement.Sql);
            }

            await transaction.CommitAsync();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<HashSet<string>> GetExistingPublicTablesAsync(AppDbContext db)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText =
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetValue(0) is string tableName && !string.IsNullOrWhiteSpace(tableName))
            {
                existing.Add(tableName);
            }
        }

        return existing;
    }
}

