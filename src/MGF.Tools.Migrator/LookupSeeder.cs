using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MGF.Infrastructure.Data;

namespace MGF.Tools.Migrator;

internal static class LookupSeeder
{
    private sealed record SeedStatement(string TableName, string Sql);

    // Hand-curated, stable lookup seeds (idempotent).
    // These are intentionally small and do NOT attempt to generate SQL from schema CSVs.
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

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var seededTables = 0;
        var skippedMissingTables = 0;

        try
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var migrationPublicTables = GetPublicTablesInMigrations(db);
            var existingPublicTables = await GetExistingPublicTablesAsync(db, cancellationToken);

            foreach (var statement in SeedStatements)
            {
                if (!migrationPublicTables.Contains(statement.TableName))
                {
                    continue;
                }

                if (!existingPublicTables.Contains(statement.TableName))
                {
                    Console.WriteLine($"MGF.Tools.Migrator: seed skipped (missing table): {statement.TableName}");
                    skippedMissingTables++;
                    continue;
                }

                await db.Database.ExecuteSqlRawAsync(statement.Sql, cancellationToken);
                Console.WriteLine($"MGF.Tools.Migrator: seeded lookup table: {statement.TableName}");
                seededTables++;
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        Console.WriteLine(
            $"MGF.Tools.Migrator: lookup seeding complete (seeded={seededTables}, skipped_missing={skippedMissingTables})."
        );
    }

    private static async Task<HashSet<string>> GetExistingPublicTablesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText =
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetValue(0) is string tableName && !string.IsNullOrWhiteSpace(tableName))
            {
                existing.Add(tableName);
            }
        }

        return existing;
    }

    private static HashSet<string> GetPublicTablesInMigrations(AppDbContext db)
    {
        var activeProvider = db.Database.ProviderName;
        if (string.IsNullOrWhiteSpace(activeProvider))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var migrationsAssembly = db.Database.GetService<IMigrationsAssembly>();
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var migrationEntry in migrationsAssembly.Migrations.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            var migration = migrationsAssembly.CreateMigration(migrationEntry.Value, activeProvider);

            foreach (var op in migration.UpOperations)
            {
                switch (op)
                {
                    case CreateTableOperation create when IsPublicSchema(create.Schema):
                        tables.Add(create.Name);
                        break;
                    case RenameTableOperation rename:
                    {
                        var schema = rename.NewSchema ?? rename.Schema;
                        if (!IsPublicSchema(schema))
                        {
                            break;
                        }

                        var newName = rename.NewName ?? rename.Name;
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            tables.Add(newName);
                        }

                        break;
                    }
                    case DropTableOperation drop when IsPublicSchema(drop.Schema):
                        tables.Remove(drop.Name);
                        break;
                }
            }
        }

        return tables;
    }

    private static bool IsPublicSchema(string? schema)
    {
        return string.IsNullOrWhiteSpace(schema) || string.Equals(schema, "public", StringComparison.OrdinalIgnoreCase);
    }
}
