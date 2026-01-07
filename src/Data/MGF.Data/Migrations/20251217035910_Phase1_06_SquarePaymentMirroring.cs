using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_06_SquarePaymentMirroring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "square_sync_review_queue",
                schema: "public",
                columns: table => new
                {
                    square_sync_review_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "open"),
                    review_type = table.Column<string>(type: "text", nullable: false),
                    processor_key = table.Column<string>(type: "text", nullable: false),
                    processor_payment_id = table.Column<string>(type: "text", nullable: false),
                    square_event_id = table.Column<string>(type: "text", nullable: false),
                    square_customer_id = table.Column<string>(type: "text", nullable: true),
                    payload = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_square_sync_review_queue", x => x.square_sync_review_id);
                    table.CheckConstraint("CK_square_sync_review_queue_0", "status IN ('open','resolved','ignored')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_processor_key_processor_payment_id",
                table: "payments",
                columns: new[] { "processor_key", "processor_payment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_square_sync_review_queue_review_type_processor_key_processo~",
                schema: "public",
                table: "square_sync_review_queue",
                columns: new[] { "review_type", "processor_key", "processor_payment_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "square_sync_review_queue",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_payments_processor_key_processor_payment_id",
                table: "payments");
        }
    }
}

