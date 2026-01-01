namespace MGF.Data.Stores.Counters;

using Microsoft.EntityFrameworkCore;
using MGF.Data.Data;

public sealed class CounterAllocator : ICounterAllocator
{
    private readonly AppDbContext db;

    public CounterAllocator(AppDbContext db)
    {
        this.db = db;
    }

    public Task<string> AllocateProjectCodeAsync(CancellationToken cancellationToken = default)
    {
        return db.Database
            .SqlQueryRaw<string>(CounterSql.AllocateProjectCodeQuery)
            .SingleAsync(cancellationToken);
    }

    public async Task<string> AllocateInvoiceNumberAsync(
        short year2,
        CancellationToken cancellationToken = default)
    {
        var seq = await db.Database
            .SqlQuery<int>(CounterSql.BuildAllocateInvoiceNumberQuery(year2))
            .SingleAsync(cancellationToken);

        return $"MGF-INV-{year2:00}-{seq:000000}";
    }
}

internal static class CounterSql
{
    internal const string AllocateProjectCodeQuery =
        """
        WITH ensured AS (
          INSERT INTO public.project_code_counters(prefix, year_2, next_seq)
          VALUES ('MGF', (EXTRACT(YEAR FROM now())::int % 100)::smallint, 1)
          ON CONFLICT (prefix, year_2) DO NOTHING
        ),
        updated AS (
          UPDATE public.project_code_counters
          SET next_seq = next_seq + 1, updated_at = now()
          WHERE prefix = 'MGF' AND year_2 = (EXTRACT(YEAR FROM now())::int % 100)::smallint
          RETURNING year_2, (next_seq - 1) AS allocated_seq
        )
        SELECT 'MGF' || lpad(year_2::text, 2, '0') || '-' || lpad(allocated_seq::text, 4, '0')
        FROM updated;
        """;

    internal static FormattableString BuildAllocateInvoiceNumberQuery(short year2)
    {
        return $"""
        WITH ensured AS (
          INSERT INTO public.invoice_number_counters(prefix, year_2, next_seq)
          VALUES ('MGF', {year2}, 1)
          ON CONFLICT (prefix, year_2) DO NOTHING
        ),
        updated AS (
          UPDATE public.invoice_number_counters
          SET next_seq = next_seq + 1, updated_at = now()
          WHERE prefix = 'MGF' AND year_2 = {year2}
          RETURNING (next_seq - 1) AS allocated_seq
        )
        SELECT allocated_seq
        FROM updated;
        """;
    }
}
