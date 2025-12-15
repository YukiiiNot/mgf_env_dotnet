using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Projects_RealModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dev-only: reset core tables for a clean v1 model.
            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "people");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    cli_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.cli_id);
                });

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    per_id = table.Column<string>(type: "text", nullable: false),
                    initials = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.per_id);
                });

            migrationBuilder.CreateTable(
                name: "project_code_counters",
                columns: table => new
                {
                    year = table.Column<int>(type: "integer", nullable: false),
                    next_seq = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_code_counters", x => x.year);
                });

            migrationBuilder.CreateTable(
                name: "project_phases",
                columns: table => new
                {
                    phase_key = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_phases", x => x.phase_key);
                });

            migrationBuilder.CreateTable(
                name: "project_priorities",
                columns: table => new
                {
                    priority_key = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_priorities", x => x.priority_key);
                });

            migrationBuilder.CreateTable(
                name: "project_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_statuses", x => x.status_key);
                });

            migrationBuilder.CreateTable(
                name: "project_types",
                columns: table => new
                {
                    type_key = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_types", x => x.type_key);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    prj_id = table.Column<string>(type: "text", nullable: false),
                    project_code = table.Column<string>(type: "text", nullable: false),
                    cli_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status_key = table.Column<string>(type: "text", nullable: false),
                    phase_key = table.Column<string>(type: "text", nullable: false),
                    priority_key = table.Column<string>(type: "text", nullable: true),
                    type_key = table.Column<string>(type: "text", nullable: true),
                    paths_root_key = table.Column<string>(type: "text", nullable: false),
                    folder_relpath = table.Column<string>(type: "text", nullable: false),
                    dropbox_url = table.Column<string>(type: "text", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.prj_id);
                    table.ForeignKey(
                        name: "FK_projects_clients_cli_id",
                        column: x => x.cli_id,
                        principalTable: "clients",
                        principalColumn: "cli_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_projects_project_phases_phase_key",
                        column: x => x.phase_key,
                        principalTable: "project_phases",
                        principalColumn: "phase_key",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_projects_project_priorities_priority_key",
                        column: x => x.priority_key,
                        principalTable: "project_priorities",
                        principalColumn: "priority_key");
                    table.ForeignKey(
                        name: "FK_projects_project_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "project_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_projects_project_types_type_key",
                        column: x => x.type_key,
                        principalTable: "project_types",
                        principalColumn: "type_key");
                });

            migrationBuilder.CreateIndex(
                name: "IX_projects_cli_id",
                table: "projects",
                column: "cli_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_phase_key",
                table: "projects",
                column: "phase_key");

            migrationBuilder.CreateIndex(
                name: "IX_projects_priority_key",
                table: "projects",
                column: "priority_key");

            migrationBuilder.CreateIndex(
                name: "IX_projects_project_code",
                table: "projects",
                column: "project_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_status_key",
                table: "projects",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_projects_type_key",
                table: "projects",
                column: "type_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "project_code_counters");

            migrationBuilder.DropTable(
                name: "project_phases");

            migrationBuilder.DropTable(
                name: "project_priorities");

            migrationBuilder.DropTable(
                name: "project_statuses");

            migrationBuilder.DropTable(
                name: "project_types");

            migrationBuilder.DropTable(
                name: "people");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.client_id);
                });

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    person_id = table.Column<string>(type: "text", nullable: false),
                    initials = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.person_id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    project_id = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.project_id);
                    table.ForeignKey(
                        name: "FK_projects_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "client_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_projects_client_id",
                table: "projects",
                column: "client_id");
        }
    }
}
