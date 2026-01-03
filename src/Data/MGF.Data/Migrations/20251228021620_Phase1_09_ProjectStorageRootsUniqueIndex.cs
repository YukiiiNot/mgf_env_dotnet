using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_09_ProjectStorageRootsUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_project_storage_roots_project_id_storage_provider_key_root_~",
                table: "project_storage_roots",
                columns: new[] { "project_id", "storage_provider_key", "root_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_storage_roots_project_id_storage_provider_key_root_~",
                table: "project_storage_roots");
        }
    }
}

