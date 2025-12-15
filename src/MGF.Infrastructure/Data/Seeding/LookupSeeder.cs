using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace MGF.Infrastructure.Data.Seeding;

public static class LookupSeeder
{
    private sealed record SeedStatement(string TableName, string Sql);

    // Hand-curated, stable lookup seeds (idempotent).
    // Keep these small and stable; migrations remain the schema source of truth.
    private static readonly SeedStatement[] SeedStatements =
    [
        UpsertKeyDisplaySort(
            tableName: "data_profiles",
            keyColumn: "profile_key",
            rows:
            [
                ("real", "Real", 10),
                ("dummy", "Dummy", 20),
                ("fixture", "Fixture", 30),
            ]
        ),
        UpsertCurrency(
            rows:
            [
                ("USD", "US Dollar", "$", 2),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "client_types",
            keyColumn: "type_key",
            rows:
            [
                ("individual", "Individual", 10),
                ("organization", "Organization", 20),
                ("agency", "Agency", 30),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "client_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("prospect", "Prospect", 10),
                ("active", "Active", 20),
                ("inactive", "Inactive", 80),
                ("archived", "Archived", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "person_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("active", "Active", 10),
                ("inactive", "Inactive", 80),
                ("archived", "Archived", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "project_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("active", "Active", 10),
                ("archived", "Archived", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "project_phases",
            keyColumn: "phase_key",
            rows:
            [
                ("planning", "Planning", 10),
                ("scheduling", "Scheduling", 20),
                ("production", "Production", 30),
                ("editing", "Editing", 40),
                ("delivery", "Delivery", 50),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "project_priorities",
            keyColumn: "priority_key",
            rows:
            [
                ("low", "Low", 10),
                ("normal", "Normal", 20),
                ("high", "High", 30),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "booking_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("pending", "Pending", 10),
                ("confirmed", "Confirmed", 20),
                ("cancelled", "Cancelled", 80),
                ("completed", "Completed", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "booking_phases",
            keyColumn: "phase_key",
            rows:
            [
                ("scheduling", "Scheduling", 10),
                ("prep", "Prep", 20),
                ("shoot", "Shoot", 30),
                ("post", "Post", 40),
                ("client_meeting", "Client Meeting", 50),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "deliverable_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("pending", "Pending", 10),
                ("sent", "Sent", 20),
                ("in_review", "In Review", 30),
                ("changes_requested", "Changes Requested", 40),
                ("approved", "Approved", 50),
                ("delivered", "Delivered", 60),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "delivery_methods",
            keyColumn: "method_key",
            rows:
            [
                ("dropbox", "Dropbox", 10),
                ("frameio", "Frame.io", 20),
                ("gdrive", "Google Drive", 30),
                ("supabase_storage", "Supabase Storage", 40),
                ("nas_share", "NAS Share", 50),
                ("other", "Other", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "payment_methods",
            keyColumn: "method_key",
            rows:
            [
                ("square", "Square", 10),
                ("cash", "Cash", 20),
                ("bank_transfer", "Bank Transfer", 30),
                ("check", "Check", 40),
                ("other", "Other", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "payment_processors",
            keyColumn: "processor_key",
            rows:
            [
                ("square", "Square", 10),
                ("manual", "Manual", 20),
                ("stripe", "Stripe", 30),
                ("other", "Other", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "payment_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("authorized", "Authorized", 10),
                ("captured", "Captured", 20),
                ("refunded", "Refunded", 30),
                ("failed", "Failed", 80),
                ("pending", "Pending", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "invoice_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("draft", "Draft", 10),
                ("unpaid", "Unpaid", 20),
                ("paid", "Paid", 30),
                ("overdue", "Overdue", 40),
                ("refunded", "Refunded", 80),
                ("void", "Void", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "integration_sync_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("ok", "OK", 10),
                ("pending", "Pending", 20),
                ("error", "Error", 80),
                ("disabled", "Disabled", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "lead_sources",
            keyColumn: "source_key",
            rows:
            [
                ("instagram", "Instagram", 10),
                ("website", "Website", 20),
                ("referral", "Referral", 30),
                ("cold_email", "Cold Email", 40),
                ("upwork", "Upwork", 50),
                ("other", "Other", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "lead_priorities",
            keyColumn: "priority_key",
            rows:
            [
                ("low", "Low", 10),
                ("normal", "Normal", 20),
                ("high", "High", 30),
                ("urgent", "Urgent", 40),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "lead_stages",
            keyColumn: "stage_key",
            rows:
            [
                ("new", "New", 10),
                ("contacted", "Contacted", 20),
                ("qualified", "Qualified", 30),
                ("proposal_sent", "Proposal Sent", 40),
                ("negotiation", "Negotiation", 50),
                ("won", "Won", 90),
                ("lost", "Lost", 95),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "lead_outcomes",
            keyColumn: "outcome_key",
            rows:
            [
                ("open", "Open", 10),
                ("won", "Won", 90),
                ("lost", "Lost", 95),
                ("closed", "Closed", 99),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "work_item_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("pending", "Pending", 10),
                ("in_progress", "In Progress", 20),
                ("blocked", "Blocked", 30),
                ("done", "Done", 90),
                ("cancelled", "Cancelled", 99),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "work_item_priorities",
            keyColumn: "priority_key",
            rows:
            [
                ("low", "Low", 10),
                ("normal", "Normal", 20),
                ("high", "High", 30),
                ("urgent", "Urgent", 40),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "job_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("queued", "Queued", 10),
                ("running", "Running", 20),
                ("succeeded", "Succeeded", 90),
                ("failed", "Failed", 95),
                ("cancelled", "Cancelled", 99),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "job_priorities",
            keyColumn: "priority_key",
            rows:
            [
                ("low", "Low", 10),
                ("normal", "Normal", 20),
                ("high", "High", 30),
                ("urgent", "Urgent", 40),
            ]
        ),
        UpsertJobTypes(
            rows:
            [
                ("dropbox.create_project_structure", "Dropbox: Create Project Structure"),
                ("notion.sync_booking", "Notion: Sync Booking"),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "activity_statuses",
            keyColumn: "status_key",
            rows:
            [
                ("info", "Info", 10),
                ("warn", "Warn", 20),
                ("error", "Error", 30),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "activity_priorities",
            keyColumn: "priority_key",
            rows:
            [
                ("low", "Low", 10),
                ("normal", "Normal", 20),
                ("high", "High", 30),
                ("urgent", "Urgent", 40),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "activity_ops",
            keyColumn: "op_key",
            rows:
            [
                ("created", "Created", 10),
                ("updated", "Updated", 20),
                ("deleted", "Deleted", 30),
                ("synced", "Synced", 40),
                ("failed", "Failed", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "activity_topics",
            keyColumn: "topic_key",
            rows:
            [
                ("projects", "Projects", 10),
                ("invoices", "Invoices", 20),
                ("payments", "Payments", 30),
                ("daemon.sync", "Daemon: Sync", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "path_anchors",
            keyColumn: "anchor_key",
            rows:
            [
                ("dropbox_root", "Dropbox Root", 10),
                ("project_root", "Project Root", 20),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "path_types",
            keyColumn: "type_key",
            rows:
            [
                ("project_root", "Project Root", 10),
                ("ingest", "Ingest", 20),
                ("edit", "Edit", 30),
                ("assets", "Assets", 40),
                ("exports", "Exports", 50),
                ("deliveries", "Deliveries", 60),
                ("invoices", "Invoices", 70),
            ]
        ),
        UpsertKeyDisplay(
            tableName: "tags",
            keyColumn: "tag_key",
            rows:
            [
                ("warm_lead", "Warm Lead"),
            ]
        ),
        UpsertKeyDisplay(
            tableName: "host_keys",
            keyColumn: "host_key",
            rows:
            [
                ("local_machine", "Local Machine"),
                ("nas_home", "NAS (Home)"),
                ("dropbox_remote", "Dropbox (Remote)"),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "storage_providers",
            keyColumn: "provider_key",
            rows:
            [
                ("dropbox", "Dropbox", 10),
                ("nas", "NAS", 20),
                ("supabase_storage", "Supabase Storage", 30),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "slug_scopes",
            keyColumn: "scope_key",
            rows:
            [
                ("project", "Project", 10),
                ("client", "Client", 20),
                ("notion_page", "Notion Page", 30),
                ("delivery", "Delivery", 40),
                ("invoice", "Invoice", 50),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "square_customer_creation_sources",
            keyColumn: "source_key",
            rows:
            [
                ("import", "Import", 10),
                ("directory", "Directory", 20),
                ("third_party", "Third Party", 30),
                ("merge", "Merge", 40),
                ("manual", "Manual", 50),
                ("instant_profile", "Instant Profile", 60),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "roles",
            keyColumn: "role_key",
            rows:
            [
                ("producer", "Producer", 10),
                ("editor", "Editor", 20),
                ("assistant", "Assistant", 30),
                ("admin", "Admin", 90),
            ]
        ),
        UpsertKeyDisplaySort(
            tableName: "role_scopes",
            keyColumn: "scope_key",
            rows:
            [
                ("person", "Person", 10),
                ("project", "Project", 20),
                ("client_contact", "Client Contact", 30),
                ("booking_attendee", "Booking Attendee", 40),
            ]
        ),
        UpsertRoleScopeRoles(
            rows:
            [
                ("project", "producer"),
                ("project", "editor"),
                ("project", "assistant"),
            ]
        ),
        UpsertServicePackages(
            rows:
            [
                ("MGF_EDIT_STARTER", "Editing Starter Package"),
            ]
        ),
        UpsertProjectCodeCounters(),
        UpsertInvoiceNumberCounters(),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var seededStatements = 0;
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
                seededStatements++;
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        Console.WriteLine(
            $"MGF.Tools.Migrator: lookup seeding complete (seeded={seededStatements}, skipped_missing={skippedMissingTables})."
        );
    }

    private static SeedStatement UpsertKeyDisplaySort(
        string tableName,
        string keyColumn,
        IReadOnlyList<(string Key, string DisplayName, int SortOrder)> rows
    )
    {
        var values = string.Join(
            ",\n  ",
            rows.Select(r => $"('{Esc(r.Key)}', '{Esc(r.DisplayName)}', {r.SortOrder})")
        );

        var sql =
            $"""
            INSERT INTO public.{tableName} ({keyColumn}, display_name, sort_order)
            VALUES
              {values}
            ON CONFLICT ({keyColumn}) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """;

        return new SeedStatement(tableName, sql);
    }

    private static SeedStatement UpsertKeyDisplay(
        string tableName,
        string keyColumn,
        IReadOnlyList<(string Key, string DisplayName)> rows
    )
    {
        var values = string.Join(
            ",\n  ",
            rows.Select(r => $"('{Esc(r.Key)}', '{Esc(r.DisplayName)}')")
        );

        var sql =
            $"""
            INSERT INTO public.{tableName} ({keyColumn}, display_name)
            VALUES
              {values}
            ON CONFLICT ({keyColumn}) DO UPDATE
            SET display_name = EXCLUDED.display_name;
            """;

        return new SeedStatement(tableName, sql);
    }

    private static SeedStatement UpsertCurrency(IReadOnlyList<(string Code, string Name, string Symbol, int MinorUnits)> rows)
    {
        const string tableName = "currencies";

        var values = string.Join(
            ",\n  ",
            rows.Select(r => $"('{Esc(r.Code)}', '{Esc(r.Name)}', '{Esc(r.Symbol)}', {r.MinorUnits})")
        );

        var sql =
            $"""
            INSERT INTO public.{tableName} (currency_code, display_name, symbol, minor_units)
            VALUES
              {values}
            ON CONFLICT (currency_code) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                symbol = EXCLUDED.symbol,
                minor_units = EXCLUDED.minor_units;
            """;

        return new SeedStatement(tableName, sql);
    }

    private static SeedStatement UpsertJobTypes(IReadOnlyList<(string JobTypeKey, string DisplayName)> rows)
    {
        const string tableName = "job_types";

        var values = string.Join(
            ",\n  ",
            rows.Select(r => $"('{Esc(r.JobTypeKey)}', '{Esc(r.DisplayName)}')")
        );

        var sql =
            $"""
            INSERT INTO public.{tableName} (job_type_key, display_name)
            VALUES
              {values}
            ON CONFLICT (job_type_key) DO UPDATE
            SET display_name = EXCLUDED.display_name;
            """;

        return new SeedStatement(tableName, sql);
    }

    private static SeedStatement UpsertRoleScopeRoles(IReadOnlyList<(string ScopeKey, string RoleKey)> rows)
    {
        const string tableName = "role_scope_roles";

        var values = string.Join(
            ",\n  ",
            rows.Select(r => $"('{Esc(r.ScopeKey)}', '{Esc(r.RoleKey)}')")
        );

        var sql =
            $"""
            INSERT INTO public.{tableName} (scope_key, role_key)
            VALUES
              {values}
            ON CONFLICT (scope_key, role_key) DO NOTHING;
            """;

        return new SeedStatement(tableName, sql);
    }

    private static SeedStatement UpsertServicePackages(IReadOnlyList<(string PackageKey, string DisplayName)> rows)
    {
        const string tableName = "service_packages";

        var values = string.Join(
            ",\n  ",
            rows.Select(r => $"('{Esc(r.PackageKey)}', '{Esc(r.DisplayName)}')")
        );

        var sql =
            $"""
            INSERT INTO public.{tableName} (package_key, display_name)
            VALUES
              {values}
            ON CONFLICT (package_key) DO UPDATE
            SET display_name = EXCLUDED.display_name;
            """;

        return new SeedStatement(tableName, sql);
    }

    private static SeedStatement UpsertProjectCodeCounters()
    {
        const string tableName = "project_code_counters";

        var sql =
            """
            INSERT INTO public.project_code_counters (prefix, year_2, next_seq)
            VALUES ('MGF', (EXTRACT(YEAR FROM now())::int % 100)::smallint, 1)
            ON CONFLICT (prefix, year_2) DO UPDATE
            SET next_seq = GREATEST(project_code_counters.next_seq, EXCLUDED.next_seq);
            """;

        return new SeedStatement(tableName, sql);
    }

    private static SeedStatement UpsertInvoiceNumberCounters()
    {
        const string tableName = "invoice_number_counters";

        var sql =
            """
            INSERT INTO public.invoice_number_counters (prefix, year_2, next_seq)
            VALUES ('MGF', (EXTRACT(YEAR FROM now())::int % 100)::smallint, 1)
            ON CONFLICT (prefix, year_2) DO UPDATE
            SET next_seq = GREATEST(invoice_number_counters.next_seq, EXCLUDED.next_seq);
            """;

        return new SeedStatement(tableName, sql);
    }

    private static string Esc(string value) => value.Replace("'", "''", StringComparison.Ordinal);

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
