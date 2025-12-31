using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_05_SquareWebhookEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "square_webhook_events",
                schema: "public",
                columns: table => new
                {
                    square_webhook_event_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    square_event_id = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    object_type = table.Column<string>(type: "text", nullable: true),
                    object_id = table.Column<string>(type: "text", nullable: true),
                    location_id = table.Column<string>(type: "text", nullable: true),
                    payload = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "received"),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_square_webhook_events", x => x.square_webhook_event_id);
                    table.CheckConstraint("CK_square_webhook_events_0", "status IN ('received','processed','failed')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_square_webhook_events_square_event_id",
                schema: "public",
                table: "square_webhook_events",
                column: "square_event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "square_webhook_events",
                schema: "public");
        }
    }
}
