using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_10_StorageRootContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "storage_root_contracts",
                columns: table => new
                {
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    root_key = table.Column<string>(type: "text", nullable: false),
                    allowed_extras = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    allowed_root_files = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    contract_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    max_bytes = table.Column<long>(type: "bigint", nullable: true),
                    max_items = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    optional_folders = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    quarantine_relpath = table.Column<string>(type: "text", nullable: true),
                    required_folders = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_root_contracts", x => new { x.provider_key, x.root_key });
                    table.CheckConstraint("CK_storage_root_contracts_0", "root_key ~ '^[a-z0-9_]+$'");
                    table.CheckConstraint("CK_storage_root_contracts_1", "contract_key ~ '^[a-z0-9_]+$'");
                    table.CheckConstraint("CK_storage_root_contracts_2", "quarantine_relpath IS NULL OR (quarantine_relpath !~ '^(\\/|[A-Za-z]:\\\\)' AND quarantine_relpath !~ '\\\\' AND quarantine_relpath !~ '(?:^|/)\\.\\.(?:/|$)')");
                    table.ForeignKey(
                        name: "FK_storage_root_contracts_storage_providers_provider_key",
                        column: x => x.provider_key,
                        principalTable: "storage_providers",
                        principalColumn: "provider_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_storage_root_contracts_contract_key",
                table: "storage_root_contracts",
                column: "contract_key");

            migrationBuilder.CreateIndex(
                name: "IX_storage_root_contracts_provider_key",
                table: "storage_root_contracts",
                column: "provider_key");

            migrationBuilder.CreateIndex(
                name: "IX_storage_root_contracts_root_key",
                table: "storage_root_contracts",
                column: "root_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storage_root_contracts");
        }
    }
}
