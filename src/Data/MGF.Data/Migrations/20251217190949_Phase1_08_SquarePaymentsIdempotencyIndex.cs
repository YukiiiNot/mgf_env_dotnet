using MGF.Data.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20251217190949_Phase1_08_SquarePaymentsIdempotencyIndex")]
public partial class Phase1_08_SquarePaymentsIdempotencyIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
              dup_count integer;
              sample_pairs text;
            BEGIN
              SELECT COUNT(*) INTO dup_count
              FROM (
                SELECT processor_key, processor_payment_id
                FROM public.payments
                WHERE processor_key IS NOT NULL AND processor_payment_id IS NOT NULL
                GROUP BY processor_key, processor_payment_id
                HAVING COUNT(*) > 1
              ) d;

              IF dup_count > 0 THEN
                SELECT string_agg(processor_key || ':' || processor_payment_id, ', ' ORDER BY processor_key, processor_payment_id)
                INTO sample_pairs
                FROM (
                  SELECT processor_key, processor_payment_id
                  FROM public.payments
                  WHERE processor_key IS NOT NULL AND processor_payment_id IS NOT NULL
                  GROUP BY processor_key, processor_payment_id
                  HAVING COUNT(*) > 1
                  ORDER BY processor_key, processor_payment_id
                  LIMIT 20
                ) s;

                RAISE EXCEPTION
                  'Duplicate payments detected for (processor_key, processor_payment_id). Cannot apply idempotency index. duplicates=%; example_pairs=%',
                  dup_count,
                  COALESCE(sample_pairs, '(none)');
              END IF;
            END $$;
            """
        );

        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS public."IX_payments_processor_key_processor_payment_id";

            CREATE UNIQUE INDEX "IX_payments_processor_key_processor_payment_id"
            ON public.payments (processor_key, processor_payment_id)
            WHERE processor_payment_id IS NOT NULL;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS public."IX_payments_processor_key_processor_payment_id";

            CREATE UNIQUE INDEX "IX_payments_processor_key_processor_payment_id"
            ON public.payments (processor_key, processor_payment_id);
            """
        );
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        AppDbContextModelBuilder.Apply(modelBuilder);
    }
}


