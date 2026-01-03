using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_03_Joins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_attendees",
                columns: table => new
                {
                    booking_attendee_id = table.Column<string>(type: "text", nullable: false),
                    booking_id = table.Column<string>(type: "text", nullable: false),
                    call_time = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    notes = table.Column<string>(type: "text", nullable: true),
                    person_id = table.Column<string>(type: "text", nullable: false),
                    release_time = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    role_key = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_attendees", x => x.booking_attendee_id);
                    table.CheckConstraint("CK_booking_attendees_0", "booking_attendee_id ~ '^bka_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.CheckConstraint("CK_booking_attendees_1", "release_time IS NULL OR call_time IS NULL OR release_time >= call_time");
                    table.ForeignKey(
                        name: "FK_booking_attendees_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_attendees_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_attendees_roles_role_key",
                        column: x => x.role_key,
                        principalTable: "roles",
                        principalColumn: "role_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_contacts",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "text", nullable: false),
                    person_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    role_key = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_contacts", x => new { x.client_id, x.person_id });
                    table.ForeignKey(
                        name: "FK_client_contacts_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "client_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_contacts_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_contacts_roles_role_key",
                        column: x => x.role_key,
                        principalTable: "roles",
                        principalColumn: "role_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lead_tags",
                columns: table => new
                {
                    lead_id = table.Column<string>(type: "text", nullable: false),
                    tag_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_tags", x => new { x.lead_id, x.tag_key });
                    table.ForeignKey(
                        name: "FK_lead_tags_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "lead_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lead_tags_tags_tag_key",
                        column: x => x.tag_key,
                        principalTable: "tags",
                        principalColumn: "tag_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_known_hosts",
                columns: table => new
                {
                    person_id = table.Column<string>(type: "text", nullable: false),
                    host_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_known_hosts", x => new { x.person_id, x.host_key });
                    table.ForeignKey(
                        name: "FK_person_known_hosts_host_keys_host_key",
                        column: x => x.host_key,
                        principalTable: "host_keys",
                        principalColumn: "host_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_known_hosts_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_permissions",
                columns: table => new
                {
                    person_id = table.Column<string>(type: "text", nullable: false),
                    permission_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_permissions", x => new { x.person_id, x.permission_key });
                    table.ForeignKey(
                        name: "FK_person_permissions_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_permissions_permissions_permission_key",
                        column: x => x.permission_key,
                        principalTable: "permissions",
                        principalColumn: "permission_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_roles",
                columns: table => new
                {
                    person_id = table.Column<string>(type: "text", nullable: false),
                    role_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_roles", x => new { x.person_id, x.role_key });
                    table.ForeignKey(
                        name: "FK_person_roles_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_roles_roles_role_key",
                        column: x => x.role_key,
                        principalTable: "roles",
                        principalColumn: "role_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                columns: table => new
                {
                    project_member_id = table.Column<string>(type: "text", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    person_id = table.Column<string>(type: "text", nullable: false),
                    project_id = table.Column<string>(type: "text", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    role_key = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_members", x => x.project_member_id);
                    table.CheckConstraint("CK_project_members_0", "project_member_id ~ '^prm_[A-Za-z0-9]+$'");
                    table.CheckConstraint("CK_project_members_1", "is_active = true OR released_at IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_project_members_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_members_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_members_roles_role_key",
                        column: x => x.role_key,
                        principalTable: "roles",
                        principalColumn: "role_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_scope_roles",
                columns: table => new
                {
                    scope_key = table.Column<string>(type: "text", nullable: false),
                    role_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_scope_roles", x => new { x.scope_key, x.role_key });
                    table.ForeignKey(
                        name: "FK_role_scope_roles_role_scopes_scope_key",
                        column: x => x.scope_key,
                        principalTable: "role_scopes",
                        principalColumn: "scope_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_scope_roles_roles_role_key",
                        column: x => x.role_key,
                        principalTable: "roles",
                        principalColumn: "role_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_item_tags",
                columns: table => new
                {
                    work_item_id = table.Column<string>(type: "text", nullable: false),
                    tag_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_tags", x => new { x.work_item_id, x.tag_key });
                    table.ForeignKey(
                        name: "FK_work_item_tags_tags_tag_key",
                        column: x => x.tag_key,
                        principalTable: "tags",
                        principalColumn: "tag_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_item_tags_work_items_work_item_id",
                        column: x => x.work_item_id,
                        principalTable: "work_items",
                        principalColumn: "work_item_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_attendees_booking_attendee_id",
                table: "booking_attendees",
                column: "booking_attendee_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_attendees_booking_id",
                table: "booking_attendees",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_attendees_booking_id_person_id",
                table: "booking_attendees",
                columns: new[] { "booking_id", "person_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_booking_attendees_call_time",
                table: "booking_attendees",
                column: "call_time");

            migrationBuilder.CreateIndex(
                name: "IX_booking_attendees_created_at",
                table: "booking_attendees",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_booking_attendees_person_id",
                table: "booking_attendees",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_attendees_release_time",
                table: "booking_attendees",
                column: "release_time");

            migrationBuilder.CreateIndex(
                name: "IX_booking_attendees_role_key",
                table: "booking_attendees",
                column: "role_key");

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_client_id",
                table: "client_contacts",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_created_at",
                table: "client_contacts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_is_primary",
                table: "client_contacts",
                column: "is_primary");

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_person_id",
                table: "client_contacts",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_role_key",
                table: "client_contacts",
                column: "role_key");

            migrationBuilder.CreateIndex(
                name: "IX_lead_tags_created_at",
                table: "lead_tags",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_lead_tags_lead_id",
                table: "lead_tags",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "IX_lead_tags_tag_key",
                table: "lead_tags",
                column: "tag_key");

            migrationBuilder.CreateIndex(
                name: "IX_person_known_hosts_created_at",
                table: "person_known_hosts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_person_known_hosts_host_key",
                table: "person_known_hosts",
                column: "host_key");

            migrationBuilder.CreateIndex(
                name: "IX_person_known_hosts_person_id",
                table: "person_known_hosts",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_permissions_created_at",
                table: "person_permissions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_person_permissions_permission_key",
                table: "person_permissions",
                column: "permission_key");

            migrationBuilder.CreateIndex(
                name: "IX_person_permissions_person_id",
                table: "person_permissions",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_roles_created_at",
                table: "person_roles",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_person_roles_person_id",
                table: "person_roles",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_roles_role_key",
                table: "person_roles",
                column: "role_key");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_assigned_at",
                table: "project_members",
                column: "assigned_at");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_is_active",
                table: "project_members",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_person_id",
                table: "project_members",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_project_id",
                table: "project_members",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_project_member_id",
                table: "project_members",
                column: "project_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_role_key",
                table: "project_members",
                column: "role_key");

            migrationBuilder.CreateIndex(
                name: "IX_role_scope_roles_created_at",
                table: "role_scope_roles",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_role_scope_roles_role_key",
                table: "role_scope_roles",
                column: "role_key");

            migrationBuilder.CreateIndex(
                name: "IX_role_scope_roles_scope_key",
                table: "role_scope_roles",
                column: "scope_key");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_tags_created_at",
                table: "work_item_tags",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_tags_tag_key",
                table: "work_item_tags",
                column: "tag_key");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_tags_work_item_id",
                table: "work_item_tags",
                column: "work_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_attendees");

            migrationBuilder.DropTable(
                name: "client_contacts");

            migrationBuilder.DropTable(
                name: "lead_tags");

            migrationBuilder.DropTable(
                name: "person_known_hosts");

            migrationBuilder.DropTable(
                name: "person_permissions");

            migrationBuilder.DropTable(
                name: "person_roles");

            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "role_scope_roles");

            migrationBuilder.DropTable(
                name: "work_item_tags");
        }
    }
}

