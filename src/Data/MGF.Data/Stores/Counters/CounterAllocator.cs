namespace MGF.Data.Stores.Counters;

using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        return ExecuteScalarAsync(CounterSql.AllocateProjectCodeQuery, cancellationToken);
    }

    public async Task<string> AllocateInvoiceNumberAsync(
        short year2,
        CancellationToken cancellationToken = default)
    {
        var seqString = await ExecuteScalarAsync(
            CounterSql.BuildAllocateInvoiceNumberQuery(year2).ToString(),
            cancellationToken);
        var seq = int.Parse(seqString);

        return $"MGF-INV-{year2:00}-{seq:000000}";
    }

    private async Task<string> ExecuteScalarAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (db.Database.CurrentTransaction?.GetDbTransaction() is { } transaction)
            {
                command.Transaction = transaction;
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null)
            {
                throw new InvalidOperationException("Counter allocation returned no results.");
            }

            return result.ToString() ?? throw new InvalidOperationException("Counter allocation returned null string.");
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
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
        FROM updated
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
        FROM updated
        """;
    }
}
