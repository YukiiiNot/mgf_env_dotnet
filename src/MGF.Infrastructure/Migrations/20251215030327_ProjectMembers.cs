using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_roles",
                columns: table => new
                {
                    role_key = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_roles", x => x.role_key);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                columns: table => new
                {
                    prj_id = table.Column<string>(type: "text", nullable: false),
                    per_id = table.Column<string>(type: "text", nullable: false),
                    role_key = table.Column<string>(type: "text", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    released_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_members", x => new { x.prj_id, x.per_id, x.role_key, x.assigned_at });
                    table.ForeignKey(
                        name: "FK_project_members_people_per_id",
                        column: x => x.per_id,
                        principalTable: "people",
                        principalColumn: "per_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_members_project_roles_role_key",
                        column: x => x.role_key,
                        principalTable: "project_roles",
                        principalColumn: "role_key",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_members_projects_prj_id",
                        column: x => x.prj_id,
                        principalTable: "projects",
                        principalColumn: "prj_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_members_per_id",
                table: "project_members",
                column: "per_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_prj_id_per_id_role_key",
                table: "project_members",
                columns: new[] { "prj_id", "per_id", "role_key" },
                unique: true,
                filter: "released_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_role_key",
                table: "project_members",
                column: "role_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "project_roles");
        }
    }
}
