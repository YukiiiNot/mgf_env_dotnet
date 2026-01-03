using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_01_Lookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_ops",
                columns: table => new
                {
                    op_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_ops", x => x.op_key);
                    table.CheckConstraint("CK_activity_ops_0", "op_key ~ '^[a-z0-9_.-]+$'");
                });

            migrationBuilder.CreateTable(
                name: "activity_priorities",
                columns: table => new
                {
                    priority_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_priorities", x => x.priority_key);
                    table.CheckConstraint("CK_activity_priorities_0", "priority_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "activity_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_statuses", x => x.status_key);
                    table.CheckConstraint("CK_activity_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "activity_topics",
                columns: table => new
                {
                    topic_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_topics", x => x.topic_key);
                    table.CheckConstraint("CK_activity_topics_0", "topic_key ~ '^[a-z0-9_.-]+$'");
                });

            migrationBuilder.CreateTable(
                name: "booking_phases",
                columns: table => new
                {
                    phase_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_phases", x => x.phase_key);
                    table.CheckConstraint("CK_booking_phases_0", "phase_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "booking_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_statuses", x => x.status_key);
                    table.CheckConstraint("CK_booking_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "client_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_statuses", x => x.status_key);
                    table.CheckConstraint("CK_client_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "client_types",
                columns: table => new
                {
                    type_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_types", x => x.type_key);
                    table.CheckConstraint("CK_client_types_0", "type_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    currency_code = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    minor_units = table.Column<short>(type: "smallint", nullable: true),
                    symbol = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.currency_code);
                    table.CheckConstraint("CK_currencies_0", "currency_code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_currencies_1", "minor_units IS NULL OR minor_units BETWEEN 0 AND 6");
                });

            migrationBuilder.CreateTable(
                name: "data_profiles",
                columns: table => new
                {
                    profile_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_profiles", x => x.profile_key);
                    table.CheckConstraint("CK_data_profiles_0", "profile_key IN ('real','dummy','fixture')");
                });

            migrationBuilder.CreateTable(
                name: "deliverable_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliverable_statuses", x => x.status_key);
                    table.CheckConstraint("CK_deliverable_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "delivery_methods",
                columns: table => new
                {
                    method_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_methods", x => x.method_key);
                    table.CheckConstraint("CK_delivery_methods_0", "method_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "host_keys",
                columns: table => new
                {
                    host_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    host_type_key = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<JsonElement>(type: "jsonb", nullable: true, defaultValueSql: "'{}'::jsonb"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_host_keys", x => x.host_key);
                    table.CheckConstraint("CK_host_keys_0", "host_key ~ '^[a-z0-9_]+$'");
                    table.CheckConstraint("CK_host_keys_1", "host_type_key IS NULL OR host_type_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "integration_sync_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_sync_statuses", x => x.status_key);
                    table.CheckConstraint("CK_integration_sync_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "invoice_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_statuses", x => x.status_key);
                    table.CheckConstraint("CK_invoice_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "job_priorities",
                columns: table => new
                {
                    priority_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_priorities", x => x.priority_key);
                    table.CheckConstraint("CK_job_priorities_0", "priority_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "job_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_statuses", x => x.status_key);
                    table.CheckConstraint("CK_job_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "job_types",
                columns: table => new
                {
                    job_type_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    handler = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_types", x => x.job_type_key);
                    table.CheckConstraint("CK_job_types_0", "job_type_key ~ '^[a-z0-9_.-]+$'");
                });

            migrationBuilder.CreateTable(
                name: "lead_outcomes",
                columns: table => new
                {
                    outcome_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_outcomes", x => x.outcome_key);
                    table.CheckConstraint("CK_lead_outcomes_0", "outcome_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "lead_priorities",
                columns: table => new
                {
                    priority_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_priorities", x => x.priority_key);
                    table.CheckConstraint("CK_lead_priorities_0", "priority_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "lead_sources",
                columns: table => new
                {
                    source_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_sources", x => x.source_key);
                    table.CheckConstraint("CK_lead_sources_0", "source_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "lead_stages",
                columns: table => new
                {
                    stage_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_stages", x => x.stage_key);
                    table.CheckConstraint("CK_lead_stages_0", "stage_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "path_anchors",
                columns: table => new
                {
                    anchor_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_path_anchors", x => x.anchor_key);
                    table.CheckConstraint("CK_path_anchors_0", "anchor_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "path_types",
                columns: table => new
                {
                    type_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_path_types", x => x.type_key);
                    table.CheckConstraint("CK_path_types_0", "type_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "payment_methods",
                columns: table => new
                {
                    method_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_methods", x => x.method_key);
                    table.CheckConstraint("CK_payment_methods_0", "method_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "payment_processors",
                columns: table => new
                {
                    processor_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_processors", x => x.processor_key);
                    table.CheckConstraint("CK_payment_processors_0", "processor_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "payment_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_statuses", x => x.status_key);
                    table.CheckConstraint("CK_payment_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "person_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_statuses", x => x.status_key);
                    table.CheckConstraint("CK_person_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "project_phases",
                columns: table => new
                {
                    phase_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_phases", x => x.phase_key);
                    table.CheckConstraint("CK_project_phases_0", "phase_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "project_priorities",
                columns: table => new
                {
                    priority_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_priorities", x => x.priority_key);
                    table.CheckConstraint("CK_project_priorities_0", "priority_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "project_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_statuses", x => x.status_key);
                    table.CheckConstraint("CK_project_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "role_scopes",
                columns: table => new
                {
                    scope_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_scopes", x => x.scope_key);
                    table.CheckConstraint("CK_role_scopes_0", "scope_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    role_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.role_key);
                    table.CheckConstraint("CK_roles_0", "role_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "service_packages",
                columns: table => new
                {
                    package_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    default_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_packages", x => x.package_key);
                    table.CheckConstraint("CK_service_packages_0", "package_key ~ '^[A-Z0-9_\\-]+$'");
                    table.CheckConstraint("CK_service_packages_1", "default_price IS NULL OR default_price >= 0");
                });

            migrationBuilder.CreateTable(
                name: "slug_scopes",
                columns: table => new
                {
                    scope_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slug_scopes", x => x.scope_key);
                    table.CheckConstraint("CK_slug_scopes_0", "scope_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "square_customer_creation_sources",
                columns: table => new
                {
                    source_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_square_customer_creation_sources", x => x.source_key);
                    table.CheckConstraint("CK_square_customer_creation_sources_0", "source_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "storage_providers",
                columns: table => new
                {
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    config = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_providers", x => x.provider_key);
                    table.CheckConstraint("CK_storage_providers_0", "provider_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    tag_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.tag_key);
                    table.CheckConstraint("CK_tags_0", "tag_key ~ '^[a-z0-9_\\-]+$'");
                });

            migrationBuilder.CreateTable(
                name: "work_item_priorities",
                columns: table => new
                {
                    priority_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_priorities", x => x.priority_key);
                    table.CheckConstraint("CK_work_item_priorities_0", "priority_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "work_item_statuses",
                columns: table => new
                {
                    status_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_statuses", x => x.status_key);
                    table.CheckConstraint("CK_work_item_statuses_0", "status_key ~ '^[a-z0-9_]+$'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_ops_created_at",
                table: "activity_ops",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_activity_ops_op_key",
                table: "activity_ops",
                column: "op_key");

            migrationBuilder.CreateIndex(
                name: "IX_activity_priorities_created_at",
                table: "activity_priorities",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_activity_priorities_priority_key",
                table: "activity_priorities",
                column: "priority_key");

            migrationBuilder.CreateIndex(
                name: "IX_activity_statuses_created_at",
                table: "activity_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_activity_statuses_status_key",
                table: "activity_statuses",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_activity_topics_created_at",
                table: "activity_topics",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_activity_topics_topic_key",
                table: "activity_topics",
                column: "topic_key");

            migrationBuilder.CreateIndex(
                name: "IX_booking_phases_created_at",
                table: "booking_phases",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_booking_phases_phase_key",
                table: "booking_phases",
                column: "phase_key");

            migrationBuilder.CreateIndex(
                name: "IX_booking_statuses_created_at",
                table: "booking_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_booking_statuses_status_key",
                table: "booking_statuses",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_client_statuses_created_at",
                table: "client_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_client_statuses_status_key",
                table: "client_statuses",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_client_types_created_at",
                table: "client_types",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_client_types_type_key",
                table: "client_types",
                column: "type_key");

            migrationBuilder.CreateIndex(
                name: "IX_currencies_created_at",
                table: "currencies",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_currencies_currency_code",
                table: "currencies",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "IX_data_profiles_created_at",
                table: "data_profiles",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_data_profiles_profile_key",
                table: "data_profiles",
                column: "profile_key");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_statuses_created_at",
                table: "deliverable_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_statuses_status_key",
                table: "deliverable_statuses",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_methods_created_at",
                table: "delivery_methods",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_methods_method_key",
                table: "delivery_methods",
                column: "method_key");

            migrationBuilder.CreateIndex(
                name: "IX_host_keys_created_at",
                table: "host_keys",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_host_keys_host_key",
                table: "host_keys",
                column: "host_key");

            migrationBuilder.CreateIndex(
                name: "IX_integration_sync_statuses_created_at",
                table: "integration_sync_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_integration_sync_statuses_status_key",
                table: "integration_sync_statuses",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_statuses_created_at",
                table: "invoice_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_statuses_status_key",
                table: "invoice_statuses",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_job_priorities_created_at",
                table: "job_priorities",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_job_priorities_priority_key",
                table: "job_priorities",
                column: "priority_key");

            migrationBuilder.CreateIndex(
                name: "IX_job_statuses_created_at",
                table: "job_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_job_statuses_status_key",
                table: "job_statuses",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_job_types_created_at",
                table: "job_types",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_job_types_job_type_key",
                table: "job_types",
                column: "job_type_key");

            migrationBuilder.CreateIndex(
                name: "IX_lead_outcomes_created_at",
                table: "lead_outcomes",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_lead_outcomes_outcome_key",
                table: "lead_outcomes",
                column: "outcome_key");

            migrationBuilder.CreateIndex(
                name: "IX_lead_priorities_created_at",
                table: "lead_priorities",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_lead_priorities_priority_key",
                table: "lead_priorities",
                column: "priority_key");

            migrationBuilder.CreateIndex(
                name: "IX_lead_sources_created_at",
                table: "lead_sources",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_lead_sources_source_key",
                table: "lead_sources",
                column: "source_key");

            migrationBuilder.CreateIndex(
                name: "IX_lead_stages_created_at",
                table: "lead_stages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_lead_stages_stage_key",
                table: "lead_stages",
                column: "stage_key");

            migrationBuilder.CreateIndex(
                name: "IX_path_anchors_anchor_key",
                table: "path_anchors",
                column: "anchor_key");

            migrationBuilder.CreateIndex(
                name: "IX_path_anchors_created_at",
                table: "path_anchors",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_path_types_created_at",
                table: "path_types",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_path_types_type_key",
                table: "path_types",
                column: "type_key");

            migrationBuilder.CreateIndex(
                name: "IX_payment_methods_created_at",
                table: "payment_methods",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_methods_method_key",
                table: "payment_methods",
                column: "method_key");

            migrationBuilder.CreateIndex(
                name: "IX_payment_processors_created_at",
                table: "payment_processors",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_processors_processor_key",
                table: "payment_processors",
                column: "processor_key");

            migrationBuilder.CreateIndex(
                name: "IX_payment_statuses_created_at",
                table: "payment_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_statuses_status_key",
                table: "payment_statuses",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_person_statuses_created_at",
                table: "person_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_person_statuses_status_key",
                table: "person_statuses",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_project_phases_created_at",
                table: "project_phases",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_project_phases_phase_key",
                table: "project_phases",
                column: "phase_key");

            migrationBuilder.CreateIndex(
                name: "IX_project_priorities_created_at",
                table: "project_priorities",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_project_priorities_priority_key",
                table: "project_priorities",
                column: "priority_key");

            migrationBuilder.CreateIndex(
                name: "IX_project_statuses_created_at",
                table: "project_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_project_statuses_status_key",
                table: "project_statuses",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_role_scopes_created_at",
                table: "role_scopes",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_role_scopes_scope_key",
                table: "role_scopes",
                column: "scope_key");

            migrationBuilder.CreateIndex(
                name: "IX_roles_created_at",
                table: "roles",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_roles_role_key",
                table: "roles",
                column: "role_key");

            migrationBuilder.CreateIndex(
                name: "IX_service_packages_created_at",
                table: "service_packages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_service_packages_package_key",
                table: "service_packages",
                column: "package_key");

            migrationBuilder.CreateIndex(
                name: "IX_slug_scopes_created_at",
                table: "slug_scopes",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_slug_scopes_scope_key",
                table: "slug_scopes",
                column: "scope_key");

            migrationBuilder.CreateIndex(
                name: "IX_square_customer_creation_sources_created_at",
                table: "square_customer_creation_sources",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_square_customer_creation_sources_source_key",
                table: "square_customer_creation_sources",
                column: "source_key");

            migrationBuilder.CreateIndex(
                name: "IX_storage_providers_created_at",
                table: "storage_providers",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_storage_providers_provider_key",
                table: "storage_providers",
                column: "provider_key");

            migrationBuilder.CreateIndex(
                name: "IX_tags_created_at",
                table: "tags",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_tags_tag_key",
                table: "tags",
                column: "tag_key");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_priorities_created_at",
                table: "work_item_priorities",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_priorities_priority_key",
                table: "work_item_priorities",
                column: "priority_key");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_statuses_created_at",
                table: "work_item_statuses",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_statuses_status_key",
                table: "work_item_statuses",
                column: "status_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_ops");

            migrationBuilder.DropTable(
                name: "activity_priorities");

            migrationBuilder.DropTable(
                name: "activity_statuses");

            migrationBuilder.DropTable(
                name: "activity_topics");

            migrationBuilder.DropTable(
                name: "booking_phases");

            migrationBuilder.DropTable(
                name: "booking_statuses");

            migrationBuilder.DropTable(
                name: "client_statuses");

            migrationBuilder.DropTable(
                name: "client_types");

            migrationBuilder.DropTable(
                name: "currencies");

            migrationBuilder.DropTable(
                name: "data_profiles");

            migrationBuilder.DropTable(
                name: "deliverable_statuses");

            migrationBuilder.DropTable(
                name: "delivery_methods");

            migrationBuilder.DropTable(
                name: "host_keys");

            migrationBuilder.DropTable(
                name: "integration_sync_statuses");

            migrationBuilder.DropTable(
                name: "invoice_statuses");

            migrationBuilder.DropTable(
                name: "job_priorities");

            migrationBuilder.DropTable(
                name: "job_statuses");

            migrationBuilder.DropTable(
                name: "job_types");

            migrationBuilder.DropTable(
                name: "lead_outcomes");

            migrationBuilder.DropTable(
                name: "lead_priorities");

            migrationBuilder.DropTable(
                name: "lead_sources");

            migrationBuilder.DropTable(
                name: "lead_stages");

            migrationBuilder.DropTable(
                name: "path_anchors");

            migrationBuilder.DropTable(
                name: "path_types");

            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.DropTable(
                name: "payment_processors");

            migrationBuilder.DropTable(
                name: "payment_statuses");

            migrationBuilder.DropTable(
                name: "person_statuses");

            migrationBuilder.DropTable(
                name: "project_phases");

            migrationBuilder.DropTable(
                name: "project_priorities");

            migrationBuilder.DropTable(
                name: "project_statuses");

            migrationBuilder.DropTable(
                name: "role_scopes");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "service_packages");

            migrationBuilder.DropTable(
                name: "slug_scopes");

            migrationBuilder.DropTable(
                name: "square_customer_creation_sources");

            migrationBuilder.DropTable(
                name: "storage_providers");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "work_item_priorities");

            migrationBuilder.DropTable(
                name: "work_item_statuses");
        }
    }
}

