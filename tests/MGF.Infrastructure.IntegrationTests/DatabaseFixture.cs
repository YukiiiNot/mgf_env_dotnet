using Microsoft.EntityFrameworkCore;
using MGF.Data.Configuration;
using MGF.Data.Data;
using MGF.Data.Data.Seeding;

namespace MGF.Infrastructure.IntegrationTests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var connectionString = TestDb.ResolveConnectionString();
        DatabaseConnection.EnsureDestructiveAllowedOrThrow(
            "Integration tests (will TRUNCATE core tables)",
            connectionString
        );

        await using var db = TestDb.CreateContext();
        await db.Database.MigrateAsync();

        await LookupSeeder.SeedAsync(db);
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
        var connectionString = db.Database.GetDbConnection().ConnectionString;
        DatabaseConnection.EnsureDestructiveAllowedOrThrow(
            "Integration tests reset (TRUNCATE core tables)",
            connectionString
        );

        await db.Database.OpenConnectionAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            await db.Database.ExecuteSqlRawAsync(
                """
                TRUNCATE TABLE
                  public.booking_attendees,
                  public.bookings,
                  public.project_members,
                  public.project_storage_roots,
                  public.projects,
                  public.people,
                  public.clients
                CASCADE;
                """
            );

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO public.project_code_counters (prefix, year_2, next_seq)
                VALUES ('MGF', (EXTRACT(YEAR FROM now())::int % 100)::smallint, 1)
                ON CONFLICT (prefix, year_2) DO UPDATE SET next_seq = 1;

                INSERT INTO public.invoice_number_counters (prefix, year_2, next_seq)
                VALUES ('MGF', (EXTRACT(YEAR FROM now())::int % 100)::smallint, 1)
                ON CONFLICT (prefix, year_2) DO UPDATE SET next_seq = 1;
                """
            );

            await transaction.CommitAsync();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}

