using System.Data;
using Microsoft.EntityFrameworkCore;
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
            "data_profiles",
            """
            INSERT INTO public.data_profiles (profile_key, display_name, sort_order)
            VALUES
              ('real', 'Real', 10),
              ('dummy', 'Dummy', 20),
              ('fixture', 'Fixture', 30)
            ON CONFLICT (profile_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "currencies",
            """
            INSERT INTO public.currencies (currency_code, display_name, symbol, minor_units)
            VALUES
              ('USD', 'US Dollar', '$', 2)
            ON CONFLICT (currency_code) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                symbol = EXCLUDED.symbol,
                minor_units = EXCLUDED.minor_units;
            """
        ),
        new(
            "activity_statuses",
            """
            INSERT INTO public.activity_statuses (status_key, display_name, sort_order)
            VALUES
              ('info', 'Info', 10),
              ('warn', 'Warn', 20),
              ('error', 'Error', 30)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "activity_priorities",
            """
            INSERT INTO public.activity_priorities (priority_key, display_name, sort_order)
            VALUES
              ('low', 'Low', 10),
              ('normal', 'Normal', 20),
              ('high', 'High', 30),
              ('urgent', 'Urgent', 40)
            ON CONFLICT (priority_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "activity_ops",
            """
            INSERT INTO public.activity_ops (op_key, display_name, sort_order)
            VALUES
              ('created', 'Created', 10),
              ('updated', 'Updated', 20),
              ('deleted', 'Deleted', 30),
              ('queued', 'Queued', 40),
              ('started', 'Started', 50),
              ('finished', 'Finished', 60),
              ('synced', 'Synced', 70),
              ('failed', 'Failed', 80)
            ON CONFLICT (op_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "integration_sync_statuses",
            """
            INSERT INTO public.integration_sync_statuses (status_key, display_name, sort_order, is_terminal)
            VALUES
              ('ok', 'OK', 10, FALSE),
              ('pending', 'Pending', 20, FALSE),
              ('error', 'Error', 30, FALSE),
              ('disabled', 'Disabled', 40, TRUE)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order,
                is_terminal = EXCLUDED.is_terminal;
            """
        ),
        new(
            "job_statuses",
            """
            INSERT INTO public.job_statuses (status_key, display_name, sort_order, is_terminal)
            VALUES
              ('queued', 'Queued', 10, FALSE),
              ('running', 'Running', 20, FALSE),
              ('succeeded', 'Succeeded', 30, TRUE),
              ('failed', 'Failed', 40, TRUE),
              ('cancelled', 'Cancelled', 50, TRUE)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order,
                is_terminal = EXCLUDED.is_terminal;
            """
        ),
        new(
            "job_priorities",
            """
            INSERT INTO public.job_priorities (priority_key, display_name, sort_order)
            VALUES
              ('low', 'Low', 10),
              ('normal', 'Normal', 20),
              ('high', 'High', 30),
              ('urgent', 'Urgent', 40)
            ON CONFLICT (priority_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "work_item_statuses",
            """
            INSERT INTO public.work_item_statuses (status_key, display_name, sort_order, is_terminal)
            VALUES
              ('pending', 'Pending', 10, FALSE),
              ('in_progress', 'In Progress', 20, FALSE),
              ('blocked', 'Blocked', 30, FALSE),
              ('done', 'Done', 40, TRUE),
              ('cancelled', 'Cancelled', 50, TRUE)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order,
                is_terminal = EXCLUDED.is_terminal;
            """
        ),
        new(
            "work_item_priorities",
            """
            INSERT INTO public.work_item_priorities (priority_key, display_name, sort_order)
            VALUES
              ('low', 'Low', 10),
              ('normal', 'Normal', 20),
              ('high', 'High', 30),
              ('urgent', 'Urgent', 40)
            ON CONFLICT (priority_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "booking_statuses",
            """
            INSERT INTO public.booking_statuses (status_key, display_name, sort_order, is_terminal)
            VALUES
              ('pending', 'Pending', 10, FALSE),
              ('confirmed', 'Confirmed', 20, FALSE),
              ('cancelled', 'Cancelled', 30, TRUE),
              ('completed', 'Completed', 40, TRUE)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order,
                is_terminal = EXCLUDED.is_terminal;
            """
        ),
        new(
            "booking_phases",
            """
            INSERT INTO public.booking_phases (phase_key, display_name, sort_order)
            VALUES
              ('scheduling', 'Scheduling', 10),
              ('prep', 'Prep', 20),
              ('shoot', 'Shoot', 30),
              ('post', 'Post', 40),
              ('client_meeting', 'Client Meeting', 50)
            ON CONFLICT (phase_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "payment_methods",
            """
            INSERT INTO public.payment_methods (method_key, display_name, sort_order)
            VALUES
              ('square', 'Square', 10),
              ('cash', 'Cash', 20),
              ('bank_transfer', 'Bank Transfer', 30),
              ('check', 'Check', 40),
              ('other', 'Other', 50)
            ON CONFLICT (method_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "payment_processors",
            """
            INSERT INTO public.payment_processors (processor_key, display_name, sort_order)
            VALUES
              ('square', 'Square', 10),
              ('manual', 'Manual', 20),
              ('stripe', 'Stripe', 30),
              ('other', 'Other', 40)
            ON CONFLICT (processor_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "payment_statuses",
            """
            INSERT INTO public.payment_statuses (status_key, display_name, sort_order, is_terminal)
            VALUES
              ('authorized', 'Authorized', 10, FALSE),
              ('captured', 'Captured', 20, FALSE),
              ('pending', 'Pending', 30, FALSE),
              ('refunded', 'Refunded', 40, TRUE),
              ('failed', 'Failed', 50, TRUE)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order,
                is_terminal = EXCLUDED.is_terminal;
            """
        ),
        new(
            "delivery_methods",
            """
            INSERT INTO public.delivery_methods (method_key, display_name, sort_order)
            VALUES
              ('dropbox', 'Dropbox', 10),
              ('frameio', 'Frame.io', 20),
              ('gdrive', 'Google Drive', 30),
              ('supabase_storage', 'Supabase Storage', 40),
              ('nas_share', 'NAS Share', 50),
              ('other', 'Other', 60)
            ON CONFLICT (method_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "lead_sources",
            """
            INSERT INTO public.lead_sources (source_key, display_name, sort_order)
            VALUES
              ('instagram', 'Instagram', 10),
              ('website', 'Website', 20),
              ('referral', 'Referral', 30),
              ('cold_email', 'Cold Email', 40),
              ('upwork', 'Upwork', 50),
              ('other', 'Other', 60)
            ON CONFLICT (source_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "lead_priorities",
            """
            INSERT INTO public.lead_priorities (priority_key, display_name, sort_order)
            VALUES
              ('low', 'Low', 10),
              ('normal', 'Normal', 20),
              ('high', 'High', 30),
              ('urgent', 'Urgent', 40)
            ON CONFLICT (priority_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "lead_stages",
            """
            INSERT INTO public.lead_stages (stage_key, display_name, sort_order)
            VALUES
              ('new', 'New', 10),
              ('contacted', 'Contacted', 20),
              ('qualified', 'Qualified', 30),
              ('proposal_sent', 'Proposal Sent', 40),
              ('negotiation', 'Negotiation', 50),
              ('won', 'Won', 60),
              ('lost', 'Lost', 70),
              ('closed', 'Closed', 80)
            ON CONFLICT (stage_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "lead_outcomes",
            """
            INSERT INTO public.lead_outcomes (outcome_key, display_name, sort_order)
            VALUES
              ('open', 'Open', 10),
              ('won', 'Won', 20),
              ('lost', 'Lost', 30),
              ('closed', 'Closed', 40)
            ON CONFLICT (outcome_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "person_statuses",
            """
            INSERT INTO public.person_statuses (status_key, display_name, sort_order, is_terminal)
            VALUES
              ('active', 'Active', 10, FALSE),
              ('inactive', 'Inactive', 20, FALSE),
              ('archived', 'Archived', 30, TRUE)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order,
                is_terminal = EXCLUDED.is_terminal;
            """
        ),
        new(
            "client_statuses",
            """
            INSERT INTO public.client_statuses (status_key, display_name, sort_order, is_terminal)
            VALUES
              ('prospect', 'Prospect', 10, FALSE),
              ('active', 'Active', 20, FALSE),
              ('inactive', 'Inactive', 30, FALSE),
              ('archived', 'Archived', 40, TRUE)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order,
                is_terminal = EXCLUDED.is_terminal;
            """
        ),
        new(
            "deliverable_statuses",
            """
            INSERT INTO public.deliverable_statuses (status_key, display_name, sort_order, is_terminal)
            VALUES
              ('pending', 'Pending', 10, FALSE),
              ('sent', 'Sent', 20, FALSE),
              ('in_review', 'In Review', 30, FALSE),
              ('changes_requested', 'Changes Requested', 40, FALSE),
              ('approved', 'Approved', 50, FALSE),
              ('delivered', 'Delivered', 60, TRUE),
              ('archived', 'Archived', 70, TRUE)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order,
                is_terminal = EXCLUDED.is_terminal;
            """
        ),
        new(
            "invoice_statuses",
            """
            INSERT INTO public.invoice_statuses (status_key, display_name, sort_order, is_terminal)
            VALUES
              ('draft', 'Draft', 10, FALSE),
              ('unpaid', 'Unpaid', 20, FALSE),
              ('overdue', 'Overdue', 30, FALSE),
              ('paid', 'Paid', 40, TRUE),
              ('refunded', 'Refunded', 50, TRUE),
              ('void', 'Void', 60, TRUE)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order,
                is_terminal = EXCLUDED.is_terminal;
            """
        ),
        new(
            "project_statuses",
            """
            INSERT INTO public.project_statuses (status_key, display_name, sort_order, is_terminal)
            VALUES
              ('active', 'Active', 10, FALSE)
            ON CONFLICT (status_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order,
                is_terminal = EXCLUDED.is_terminal;
            """
        ),
        new(
            "project_priorities",
            """
            INSERT INTO public.project_priorities (priority_key, display_name, sort_order)
            VALUES
              ('low', 'Low', 10),
              ('normal', 'Normal', 20),
              ('high', 'High', 30),
              ('urgent', 'Urgent', 40)
            ON CONFLICT (priority_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "role_scopes",
            """
            INSERT INTO public.role_scopes (scope_key, display_name, sort_order)
            VALUES
              ('person', 'Person', 10),
              ('project', 'Project', 20),
              ('client_contact', 'Client Contact', 30),
              ('booking_attendee', 'Booking Attendee', 40)
            ON CONFLICT (scope_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
        new(
            "roles",
            """
            INSERT INTO public.roles (role_key, display_name, sort_order)
            VALUES
              ('admin', 'Admin', 10),
              ('producer', 'Producer', 20),
              ('editor', 'Editor', 30)
            ON CONFLICT (role_key) DO UPDATE
            SET display_name = EXCLUDED.display_name,
                sort_order = EXCLUDED.sort_order;
            """
        ),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var seededTables = 0;
        var skippedTables = 0;

        try
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            foreach (var statement in SeedStatements)
            {
                if (!await TableExistsAsync(db, statement.TableName, cancellationToken))
                {
                    Console.WriteLine($"MGF.Tools.Migrator: seed skipped (missing table): {statement.TableName}");
                    skippedTables++;
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

        Console.WriteLine($"MGF.Tools.Migrator: lookup seeding complete (seeded={seededTables}, skipped={skippedTables}).");
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string tableName, CancellationToken cancellationToken)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText =
            """
            SELECT EXISTS (
              SELECT 1
              FROM information_schema.tables
              WHERE table_schema = 'public' AND table_name = @tableName
            )
            """;

        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "tableName";
        parameter.DbType = DbType.String;
        parameter.Value = tableName;
        cmd.Parameters.Add(parameter);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is bool exists && exists;
    }
}

