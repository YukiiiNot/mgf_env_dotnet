using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_07_SquarePaymentReconcile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "square_reconcile_cursors",
                schema: "public",
                columns: table => new
                {
                    reconcile_key = table.Column<string>(type: "text", nullable: false),
                    cursor_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_square_reconcile_cursors", x => x.reconcile_key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "square_reconcile_cursors",
                schema: "public");
        }
    }
}

