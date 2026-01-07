using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MGF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_02_Core : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_log",
                columns: table => new
                {
                    activity_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_by_actor = table.Column<string>(type: "text", nullable: true),
                    entity_key = table.Column<string>(type: "text", nullable: false),
                    entity_type_key = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    occurred_at_local = table.Column<string>(type: "text", nullable: true),
                    op_key = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    priority_key = table.Column<string>(type: "text", nullable: false, defaultValue: "normal"),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    source = table.Column<string>(type: "text", nullable: true),
                    status_key = table.Column<string>(type: "text", nullable: true),
                    topic_key = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_log", x => x.activity_id);
                    table.CheckConstraint("CK_activity_log_0", "activity_id ~ '^evt_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.CheckConstraint("CK_activity_log_1", "entity_type_key ~ '^[a-z0-9_]+$'");
                    table.CheckConstraint("CK_activity_log_2", "retry_count >= 0");
                    table.ForeignKey(
                        name: "FK_activity_log_activity_ops_op_key",
                        column: x => x.op_key,
                        principalTable: "activity_ops",
                        principalColumn: "op_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_log_activity_priorities_priority_key",
                        column: x => x.priority_key,
                        principalTable: "activity_priorities",
                        principalColumn: "priority_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_log_activity_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "activity_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_log_activity_topics_topic_key",
                        column: x => x.topic_key,
                        principalTable: "activity_topics",
                        principalColumn: "topic_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_number_counters",
                columns: table => new
                {
                    prefix = table.Column<string>(type: "text", nullable: false),
                    year_2 = table.Column<short>(type: "smallint", nullable: false),
                    next_seq = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_number_counters", x => new { x.prefix, x.year_2 });
                    table.CheckConstraint("CK_invoice_number_counters_0", "next_seq >= 0 AND next_seq <= 999999");
                });

            migrationBuilder.CreateTable(
                name: "path_templates",
                columns: table => new
                {
                    path_key = table.Column<string>(type: "text", nullable: false),
                    anchor_key = table.Column<string>(type: "text", nullable: false, defaultValue: "dropbox_root"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    description = table.Column<string>(type: "text", nullable: true),
                    exists_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    path_type_key = table.Column<string>(type: "text", nullable: false),
                    relpath = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    writable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_path_templates", x => x.path_key);
                    table.CheckConstraint("CK_path_templates_0", "path_key ~ '^[a-z0-9_]+$'");
                    table.CheckConstraint("CK_path_templates_1", "relpath !~ '^(\\/|[A-Za-z]:\\\\)' AND relpath !~ '\\\\' AND relpath !~ '(?:^|/)\\.\\.(?:/|$)'");
                    table.ForeignKey(
                        name: "FK_path_templates_path_anchors_anchor_key",
                        column: x => x.anchor_key,
                        principalTable: "path_anchors",
                        principalColumn: "anchor_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_path_templates_path_types_path_type_key",
                        column: x => x.path_type_key,
                        principalTable: "path_types",
                        principalColumn: "type_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    person_id = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    initials = table.Column<string>(type: "text", nullable: true),
                    status_key = table.Column<string>(type: "text", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: true),
                    default_host_key = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    data_profile = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.person_id);
                    table.CheckConstraint("CK_people_0", "person_id ~ '^per_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.CheckConstraint("CK_people_1", "char_length(initials) BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_people_data_profiles_data_profile",
                        column: x => x.data_profile,
                        principalTable: "data_profiles",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_people_host_keys_default_host_key",
                        column: x => x.default_host_key,
                        principalTable: "host_keys",
                        principalColumn: "host_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_people_person_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "person_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    permission_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.permission_key);
                    table.CheckConstraint("CK_permissions_0", "permission_key ~ '^[a-z0-9_.]+$'");
                });

            migrationBuilder.CreateTable(
                name: "project_code_counters",
                columns: table => new
                {
                    prefix = table.Column<string>(type: "text", nullable: false),
                    year_2 = table.Column<short>(type: "smallint", nullable: false),
                    next_seq = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_code_counters", x => new { x.prefix, x.year_2 });
                    table.CheckConstraint("CK_project_code_counters_0", "next_seq >= 0 AND next_seq <= 9999");
                });

            migrationBuilder.CreateTable(
                name: "activity_acknowledgements",
                columns: table => new
                {
                    activity_id = table.Column<string>(type: "text", nullable: false),
                    acknowledged_by_actor = table.Column<string>(type: "text", nullable: false),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_acknowledgements", x => new { x.activity_id, x.acknowledged_by_actor });
                    table.ForeignKey(
                        name: "FK_activity_acknowledgements_activity_log_activity_id",
                        column: x => x.activity_id,
                        principalTable: "activity_log",
                        principalColumn: "activity_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    job_id = table.Column<string>(type: "text", nullable: false),
                    activity_id = table.Column<string>(type: "text", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_by_actor = table.Column<string>(type: "text", nullable: true),
                    entity_key = table.Column<string>(type: "text", nullable: true),
                    entity_type_key = table.Column<string>(type: "text", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    job_type_key = table.Column<string>(type: "text", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    locked_by = table.Column<string>(type: "text", nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    max_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    payload = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    priority_key = table.Column<string>(type: "text", nullable: false, defaultValue: "normal"),
                    run_after = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status_key = table.Column<string>(type: "text", nullable: false, defaultValue: "queued"),
                    topic_key = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.job_id);
                    table.CheckConstraint("CK_jobs_0", "job_id ~ '^job_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.CheckConstraint("CK_jobs_1", "entity_type_key ~ '^[a-z0-9_]+$'");
                    table.CheckConstraint("CK_jobs_2", "attempt_count >= 0");
                    table.CheckConstraint("CK_jobs_3", "max_attempts >= 1");
                    table.ForeignKey(
                        name: "FK_jobs_activity_log_activity_id",
                        column: x => x.activity_id,
                        principalTable: "activity_log",
                        principalColumn: "activity_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jobs_job_priorities_priority_key",
                        column: x => x.priority_key,
                        principalTable: "job_priorities",
                        principalColumn: "priority_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jobs_job_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "job_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jobs_job_types_job_type_key",
                        column: x => x.job_type_key,
                        principalTable: "job_types",
                        principalColumn: "job_type_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "path_settings",
                columns: table => new
                {
                    settings_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    default_project_root_key = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_path_settings", x => x.settings_id);
                    table.CheckConstraint("CK_path_settings_0", "settings_id = 1");
                    table.ForeignKey(
                        name: "FK_path_settings_path_templates_default_project_root_key",
                        column: x => x.default_project_root_key,
                        principalTable: "path_templates",
                        principalColumn: "path_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    client_type_key = table.Column<string>(type: "text", nullable: false),
                    status_key = table.Column<string>(type: "text", nullable: false),
                    primary_contact_person_id = table.Column<string>(type: "text", nullable: true),
                    account_owner_person_id = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    data_profile = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.client_id);
                    table.CheckConstraint("CK_clients_0", "client_id ~ '^cli_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.ForeignKey(
                        name: "FK_clients_client_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "client_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clients_client_types_client_type_key",
                        column: x => x.client_type_key,
                        principalTable: "client_types",
                        principalColumn: "type_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clients_data_profiles_data_profile",
                        column: x => x.data_profile,
                        principalTable: "data_profiles",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clients_people_account_owner_person_id",
                        column: x => x.account_owner_person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clients_people_primary_contact_person_id",
                        column: x => x.primary_contact_person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_calendar_sync_settings",
                columns: table => new
                {
                    person_id = table.Column<string>(type: "text", nullable: false),
                    include_personal_calendar = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    personal_calendar_source = table.Column<string>(type: "text", nullable: true),
                    public_personal_events = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_calendar_sync_settings", x => x.person_id);
                    table.ForeignKey(
                        name: "FK_person_calendar_sync_settings_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_contacts",
                columns: table => new
                {
                    person_id = table.Column<string>(type: "text", nullable: false),
                    discord_handle = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_contacts", x => x.person_id);
                    table.ForeignKey(
                        name: "FK_person_contacts_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "slug_reservations",
                columns: table => new
                {
                    scope_key = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    data_profile = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    reserved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    reserved_by_person_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slug_reservations", x => new { x.scope_key, x.slug });
                    table.CheckConstraint("CK_slug_reservations_0", "slug ~ '^[a-z0-9_-]{3,}$'");
                    table.ForeignKey(
                        name: "FK_slug_reservations_data_profiles_data_profile",
                        column: x => x.data_profile,
                        principalTable: "data_profiles",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_slug_reservations_people_reserved_by_person_id",
                        column: x => x.reserved_by_person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_slug_reservations_slug_scopes_scope_key",
                        column: x => x.scope_key,
                        principalTable: "slug_scopes",
                        principalColumn: "scope_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_billing_profiles",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "text", nullable: false),
                    address_city = table.Column<string>(type: "text", nullable: true),
                    address_country = table.Column<string>(type: "text", nullable: true),
                    address_line1 = table.Column<string>(type: "text", nullable: true),
                    address_line2 = table.Column<string>(type: "text", nullable: true),
                    address_postal_code = table.Column<string>(type: "text", nullable: true),
                    address_region = table.Column<string>(type: "text", nullable: true),
                    billing_email = table.Column<string>(type: "text", nullable: true),
                    billing_phone = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    tax_id = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_billing_profiles", x => x.client_id);
                    table.ForeignKey(
                        name: "FK_client_billing_profiles_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "client_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "client_integrations_square",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "text", nullable: false),
                    creation_source_key = table.Column<string>(type: "text", nullable: true),
                    currency_code = table.Column<string>(type: "text", nullable: true),
                    first_visit_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    last_visit_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    lifetime_spend_cents = table.Column<int>(type: "integer", nullable: true),
                    square_customer_id = table.Column<string>(type: "text", nullable: true),
                    transaction_count = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_integrations_square", x => x.client_id);
                    table.CheckConstraint("CK_client_integrations_square_0", "transaction_count >= 0");
                    table.CheckConstraint("CK_client_integrations_square_1", "lifetime_spend_cents >= 0");
                    table.ForeignKey(
                        name: "FK_client_integrations_square_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "client_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_integrations_square_square_customer_creation_sources~",
                        column: x => x.creation_source_key,
                        principalTable: "square_customer_creation_sources",
                        principalColumn: "source_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    booking_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_by_person_id = table.Column<string>(type: "text", nullable: true),
                    data_profile = table.Column<string>(type: "text", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    google_event_id = table.Column<string>(type: "text", nullable: true),
                    ingest_time_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    location_city = table.Column<string>(type: "text", nullable: true),
                    location_country = table.Column<string>(type: "text", nullable: true),
                    location_line1 = table.Column<string>(type: "text", nullable: true),
                    location_line2 = table.Column<string>(type: "text", nullable: true),
                    location_name = table.Column<string>(type: "text", nullable: true),
                    location_notes = table.Column<string>(type: "text", nullable: true),
                    location_postal_code = table.Column<string>(type: "text", nullable: true),
                    location_region = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    notes = table.Column<string>(type: "text", nullable: true),
                    phase_key = table.Column<string>(type: "text", nullable: false, defaultValue: "scheduling"),
                    prep_time_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    project_id = table.Column<string>(type: "text", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    status_key = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    timezone = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    travel_time_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    wrap_time_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 90)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.booking_id);
                    table.CheckConstraint("CK_bookings_0", "booking_id ~ '^bkg_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.CheckConstraint("CK_bookings_1", "end_at > start_at");
                    table.CheckConstraint("CK_bookings_2", "prep_time_minutes >= 0");
                    table.CheckConstraint("CK_bookings_3", "travel_time_minutes >= 0");
                    table.CheckConstraint("CK_bookings_4", "wrap_time_minutes >= 0");
                    table.CheckConstraint("CK_bookings_5", "ingest_time_minutes >= 0");
                    table.ForeignKey(
                        name: "FK_bookings_booking_phases_phase_key",
                        column: x => x.phase_key,
                        principalTable: "booking_phases",
                        principalColumn: "phase_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_booking_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "booking_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_data_profiles_data_profile",
                        column: x => x.data_profile,
                        principalTable: "data_profiles",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_people_created_by_person_id",
                        column: x => x.created_by_person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deliverable_versions",
                columns: table => new
                {
                    deliverable_version_id = table.Column<string>(type: "text", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    approved_by_person_id = table.Column<string>(type: "text", nullable: true),
                    client_feedback = table.Column<string>(type: "text", nullable: true),
                    client_reviewed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    deliverable_id = table.Column<string>(type: "text", nullable: false),
                    delivery_link = table.Column<string>(type: "text", nullable: true),
                    file_path = table.Column<string>(type: "text", nullable: true),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    uploaded_by_person_id = table.Column<string>(type: "text", nullable: false),
                    version_tag = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliverable_versions", x => x.deliverable_version_id);
                    table.CheckConstraint("CK_deliverable_versions_0", "deliverable_version_id ~ '^dlvv_[A-Za-z0-9]+$'");
                    table.CheckConstraint("CK_deliverable_versions_1", "char_length(version_tag) BETWEEN 1 AND 50");
                    table.ForeignKey(
                        name: "FK_deliverable_versions_people_approved_by_person_id",
                        column: x => x.approved_by_person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deliverable_versions_people_uploaded_by_person_id",
                        column: x => x.uploaded_by_person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deliverables",
                columns: table => new
                {
                    deliverable_id = table.Column<string>(type: "text", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    approved_version_id = table.Column<string>(type: "text", nullable: true),
                    client_last_feedback = table.Column<string>(type: "text", nullable: true),
                    client_last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by_person_id = table.Column<string>(type: "text", nullable: false),
                    data_profile = table.Column<string>(type: "text", nullable: false),
                    delivery_link = table.Column<string>(type: "text", nullable: true),
                    delivery_method_key = table.Column<string>(type: "text", nullable: false),
                    delivery_note = table.Column<string>(type: "text", nullable: true),
                    invoice_id = table.Column<string>(type: "text", nullable: true),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    payment_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    project_id = table.Column<string>(type: "text", nullable: false),
                    status_key = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliverables", x => x.deliverable_id);
                    table.CheckConstraint("CK_deliverables_0", "deliverable_id ~ '^dlv_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.ForeignKey(
                        name: "FK_deliverables_data_profiles_data_profile",
                        column: x => x.data_profile,
                        principalTable: "data_profiles",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deliverables_deliverable_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "deliverable_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deliverables_deliverable_versions_approved_version_id",
                        column: x => x.approved_version_id,
                        principalTable: "deliverable_versions",
                        principalColumn: "deliverable_version_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deliverables_delivery_methods_delivery_method_key",
                        column: x => x.delivery_method_key,
                        principalTable: "delivery_methods",
                        principalColumn: "method_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deliverables_people_created_by_person_id",
                        column: x => x.created_by_person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_integrations_square",
                columns: table => new
                {
                    invoice_id = table.Column<string>(type: "text", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    square_customer_id = table.Column<string>(type: "text", nullable: true),
                    square_invoice_id = table.Column<string>(type: "text", nullable: true),
                    sync_status_key = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_integrations_square", x => x.invoice_id);
                    table.ForeignKey(
                        name: "FK_invoice_integrations_square_integration_sync_statuses_sync_~",
                        column: x => x.sync_status_key,
                        principalTable: "integration_sync_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    invoice_item_id = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    discount_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    invoice_id = table.Column<string>(type: "text", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    line_total_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_items", x => x.invoice_item_id);
                    table.CheckConstraint("CK_invoice_items_0", "invoice_item_id ~ '^ini_[A-Za-z0-9]+$'");
                    table.CheckConstraint("CK_invoice_items_1", "line_number >= 1");
                    table.CheckConstraint("CK_invoice_items_2", "quantity >= 0");
                    table.CheckConstraint("CK_invoice_items_3", "unit_price_amount >= 0");
                    table.CheckConstraint("CK_invoice_items_4", "discount_amount >= 0");
                    table.CheckConstraint("CK_invoice_items_5", "line_total_amount >= 0");
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    invoice_id = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    currency_code = table.Column<string>(type: "text", nullable: false, defaultValue: "USD"),
                    data_profile = table.Column<string>(type: "text", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    invoice_number = table.Column<string>(type: "text", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    payment_method_key = table.Column<string>(type: "text", nullable: true),
                    project_id = table.Column<string>(type: "text", nullable: false),
                    refunded_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status_key = table.Column<string>(type: "text", nullable: false, defaultValue: "unpaid"),
                    subtotal_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    tax_rate = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.invoice_id);
                    table.CheckConstraint("CK_invoices_0", "invoice_id ~ '^inv_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.CheckConstraint("CK_invoices_1", "invoice_number ~ '^MGF-INV-[0-9]{2}-[0-9]{6}$'");
                    table.CheckConstraint("CK_invoices_2", "subtotal_amount >= 0");
                    table.CheckConstraint("CK_invoices_3", "tax_rate BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_invoices_4", "tax_amount >= 0");
                    table.CheckConstraint("CK_invoices_5", "total_amount >= 0");
                    table.ForeignKey(
                        name: "FK_invoices_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "client_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_currencies_currency_code",
                        column: x => x.currency_code,
                        principalTable: "currencies",
                        principalColumn: "currency_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_data_profiles_data_profile",
                        column: x => x.data_profile,
                        principalTable: "data_profiles",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_invoice_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "invoice_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_payment_methods_payment_method_key",
                        column: x => x.payment_method_key,
                        principalTable: "payment_methods",
                        principalColumn: "method_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    payment_id = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    currency_code = table.Column<string>(type: "text", nullable: false, defaultValue: "USD"),
                    data_profile = table.Column<string>(type: "text", nullable: false),
                    invoice_id = table.Column<string>(type: "text", nullable: false),
                    method_key = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    processor_key = table.Column<string>(type: "text", nullable: true),
                    processor_payment_id = table.Column<string>(type: "text", nullable: true),
                    processor_refund_id = table.Column<string>(type: "text", nullable: true),
                    recorded_by_person_id = table.Column<string>(type: "text", nullable: true),
                    refunded_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    refunded_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    status_key = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.payment_id);
                    table.CheckConstraint("CK_payments_0", "payment_id ~ '^pay_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.CheckConstraint("CK_payments_1", "amount > 0");
                    table.CheckConstraint("CK_payments_2", "refunded_amount >= 0 AND refunded_amount <= amount");
                    table.ForeignKey(
                        name: "FK_payments_currencies_currency_code",
                        column: x => x.currency_code,
                        principalTable: "currencies",
                        principalColumn: "currency_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_data_profiles_data_profile",
                        column: x => x.data_profile,
                        principalTable: "data_profiles",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "invoice_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_payment_methods_method_key",
                        column: x => x.method_key,
                        principalTable: "payment_methods",
                        principalColumn: "method_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_payment_processors_processor_key",
                        column: x => x.processor_key,
                        principalTable: "payment_processors",
                        principalColumn: "processor_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_payment_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "payment_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_people_recorded_by_person_id",
                        column: x => x.recorded_by_person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    project_id = table.Column<string>(type: "text", nullable: false),
                    project_code = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status_key = table.Column<string>(type: "text", nullable: false),
                    phase_key = table.Column<string>(type: "text", nullable: false),
                    priority_key = table.Column<string>(type: "text", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    data_profile = table.Column<string>(type: "text", nullable: false),
                    current_invoice_id = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.project_id);
                    table.CheckConstraint("CK_projects_0", "project_id ~ '^prj_[A-Za-z0-9]+$'");
                    table.CheckConstraint("CK_projects_1", "project_code ~ '^MGF[0-9]{2}-[0-9]{4}$'");
                    table.CheckConstraint("CK_projects_2", "archived_at IS NULL OR status_key = 'archived'");
                    table.ForeignKey(
                        name: "FK_projects_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "client_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_data_profiles_data_profile",
                        column: x => x.data_profile,
                        principalTable: "data_profiles",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_invoices_current_invoice_id",
                        column: x => x.current_invoice_id,
                        principalTable: "invoices",
                        principalColumn: "invoice_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_project_phases_phase_key",
                        column: x => x.phase_key,
                        principalTable: "project_phases",
                        principalColumn: "phase_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_project_priorities_priority_key",
                        column: x => x.priority_key,
                        principalTable: "project_priorities",
                        principalColumn: "priority_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_project_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "project_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leads",
                columns: table => new
                {
                    lead_id = table.Column<string>(type: "text", nullable: false),
                    budget_estimate_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    campaign = table.Column<string>(type: "text", nullable: true),
                    company_name = table.Column<string>(type: "text", nullable: true),
                    contact_display_name = table.Column<string>(type: "text", nullable: true),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    contact_person_id = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    converted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    converted_client_id = table.Column<string>(type: "text", nullable: true),
                    converted_project_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    data_profile = table.Column<string>(type: "text", nullable: false),
                    interested_package_key = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    outcome_key = table.Column<string>(type: "text", nullable: false, defaultValue: "open"),
                    priority_key = table.Column<string>(type: "text", nullable: false, defaultValue: "normal"),
                    source_key = table.Column<string>(type: "text", nullable: true),
                    stage_key = table.Column<string>(type: "text", nullable: false, defaultValue: "new"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leads", x => x.lead_id);
                    table.CheckConstraint("CK_leads_0", "lead_id ~ '^lead_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.CheckConstraint("CK_leads_1", "budget_estimate_amount >= 0");
                    table.ForeignKey(
                        name: "FK_leads_clients_converted_client_id",
                        column: x => x.converted_client_id,
                        principalTable: "clients",
                        principalColumn: "client_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_data_profiles_data_profile",
                        column: x => x.data_profile,
                        principalTable: "data_profiles",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_lead_outcomes_outcome_key",
                        column: x => x.outcome_key,
                        principalTable: "lead_outcomes",
                        principalColumn: "outcome_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_lead_priorities_priority_key",
                        column: x => x.priority_key,
                        principalTable: "lead_priorities",
                        principalColumn: "priority_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_lead_sources_source_key",
                        column: x => x.source_key,
                        principalTable: "lead_sources",
                        principalColumn: "source_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_lead_stages_stage_key",
                        column: x => x.stage_key,
                        principalTable: "lead_stages",
                        principalColumn: "stage_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_people_contact_person_id",
                        column: x => x.contact_person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_projects_converted_project_id",
                        column: x => x.converted_project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_service_packages_interested_package_key",
                        column: x => x.interested_package_key,
                        principalTable: "service_packages",
                        principalColumn: "package_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_storage_roots",
                columns: table => new
                {
                    project_storage_root_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    folder_relpath = table.Column<string>(type: "text", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    project_id = table.Column<string>(type: "text", nullable: false),
                    root_key = table.Column<string>(type: "text", nullable: false),
                    share_url = table.Column<string>(type: "text", nullable: true),
                    storage_provider_key = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_storage_roots", x => x.project_storage_root_id);
                    table.CheckConstraint("CK_project_storage_roots_0", "project_storage_root_id ~ '^psr_[A-Za-z0-9]+$'");
                    table.CheckConstraint("CK_project_storage_roots_1", "folder_relpath !~ '(^/|\\\\\\\\|\\.\\.)'");
                    table.ForeignKey(
                        name: "FK_project_storage_roots_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_storage_roots_storage_providers_storage_provider_key",
                        column: x => x.storage_provider_key,
                        principalTable: "storage_providers",
                        principalColumn: "provider_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_items",
                columns: table => new
                {
                    work_item_id = table.Column<string>(type: "text", nullable: false),
                    assigned_to_person_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    data_profile = table.Column<string>(type: "text", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    priority_key = table.Column<string>(type: "text", nullable: false, defaultValue: "normal"),
                    project_id = table.Column<string>(type: "text", nullable: false),
                    source_activity_id = table.Column<string>(type: "text", nullable: true),
                    status_key = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    template_id = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_items", x => x.work_item_id);
                    table.CheckConstraint("CK_work_items_0", "work_item_id ~ '^tsk_[0-9a-hjkmnp-tv-z]{26}$'");
                    table.ForeignKey(
                        name: "FK_work_items_activity_log_source_activity_id",
                        column: x => x.source_activity_id,
                        principalTable: "activity_log",
                        principalColumn: "activity_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_items_data_profiles_data_profile",
                        column: x => x.data_profile,
                        principalTable: "data_profiles",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_items_people_assigned_to_person_id",
                        column: x => x.assigned_to_person_id,
                        principalTable: "people",
                        principalColumn: "person_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_items_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_items_work_item_priorities_priority_key",
                        column: x => x.priority_key,
                        principalTable: "work_item_priorities",
                        principalColumn: "priority_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_items_work_item_statuses_status_key",
                        column: x => x.status_key,
                        principalTable: "work_item_statuses",
                        principalColumn: "status_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_acknowledgements_acknowledged_at",
                table: "activity_acknowledgements",
                column: "acknowledged_at");

            migrationBuilder.CreateIndex(
                name: "IX_activity_acknowledgements_acknowledged_by_actor",
                table: "activity_acknowledgements",
                column: "acknowledged_by_actor");

            migrationBuilder.CreateIndex(
                name: "IX_activity_acknowledgements_activity_id",
                table: "activity_acknowledgements",
                column: "activity_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_log_activity_id",
                table: "activity_log",
                column: "activity_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_log_created_at",
                table: "activity_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_activity_log_created_by_actor",
                table: "activity_log",
                column: "created_by_actor");

            migrationBuilder.CreateIndex(
                name: "IX_activity_log_entity_key",
                table: "activity_log",
                column: "entity_key");

            migrationBuilder.CreateIndex(
                name: "IX_activity_log_occurred_at",
                table: "activity_log",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_activity_log_op_key",
                table: "activity_log",
                column: "op_key");

            migrationBuilder.CreateIndex(
                name: "IX_activity_log_priority_key",
                table: "activity_log",
                column: "priority_key");

            migrationBuilder.CreateIndex(
                name: "IX_activity_log_source",
                table: "activity_log",
                column: "source");

            migrationBuilder.CreateIndex(
                name: "IX_activity_log_status_key",
                table: "activity_log",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_activity_log_topic_key",
                table: "activity_log",
                column: "topic_key");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_booking_id",
                table: "bookings",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_created_at",
                table: "bookings",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_created_by_person_id",
                table: "bookings",
                column: "created_by_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_data_profile",
                table: "bookings",
                column: "data_profile");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_end_at",
                table: "bookings",
                column: "end_at");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_google_event_id",
                table: "bookings",
                column: "google_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_phase_key",
                table: "bookings",
                column: "phase_key");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_project_id",
                table: "bookings",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_start_at",
                table: "bookings",
                column: "start_at");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_status_key",
                table: "bookings",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_title",
                table: "bookings",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "IX_client_billing_profiles_client_id",
                table: "client_billing_profiles",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_billing_profiles_created_at",
                table: "client_billing_profiles",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_client_integrations_square_client_id",
                table: "client_integrations_square",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_integrations_square_creation_source_key",
                table: "client_integrations_square",
                column: "creation_source_key");

            migrationBuilder.CreateIndex(
                name: "IX_client_integrations_square_square_customer_id",
                table: "client_integrations_square",
                column: "square_customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_account_owner_person_id",
                table: "clients",
                column: "account_owner_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_client_id",
                table: "clients",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_client_type_key",
                table: "clients",
                column: "client_type_key");

            migrationBuilder.CreateIndex(
                name: "IX_clients_created_at",
                table: "clients",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_clients_data_profile",
                table: "clients",
                column: "data_profile");

            migrationBuilder.CreateIndex(
                name: "IX_clients_display_name",
                table: "clients",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "IX_clients_primary_contact_person_id",
                table: "clients",
                column: "primary_contact_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_clients_status_key",
                table: "clients",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_versions_approved_at",
                table: "deliverable_versions",
                column: "approved_at");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_versions_approved_by_person_id",
                table: "deliverable_versions",
                column: "approved_by_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_versions_client_reviewed_at",
                table: "deliverable_versions",
                column: "client_reviewed_at");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_versions_created_at",
                table: "deliverable_versions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_versions_deliverable_id",
                table: "deliverable_versions",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_versions_deliverable_version_id",
                table: "deliverable_versions",
                column: "deliverable_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_versions_is_approved",
                table: "deliverable_versions",
                column: "is_approved");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_versions_uploaded_at",
                table: "deliverable_versions",
                column: "uploaded_at");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_versions_uploaded_by_person_id",
                table: "deliverable_versions",
                column: "uploaded_by_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliverable_versions_version_tag",
                table: "deliverable_versions",
                column: "version_tag");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_approved_at",
                table: "deliverables",
                column: "approved_at");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_approved_version_id",
                table: "deliverables",
                column: "approved_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_client_last_reviewed_at",
                table: "deliverables",
                column: "client_last_reviewed_at");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_created_at",
                table: "deliverables",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_created_by_person_id",
                table: "deliverables",
                column: "created_by_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_data_profile",
                table: "deliverables",
                column: "data_profile");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_deliverable_id",
                table: "deliverables",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_delivery_method_key",
                table: "deliverables",
                column: "delivery_method_key");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_invoice_id",
                table: "deliverables",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_is_locked",
                table: "deliverables",
                column: "is_locked");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_payment_required",
                table: "deliverables",
                column: "payment_required");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_project_id",
                table: "deliverables",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_status_key",
                table: "deliverables",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_integrations_square_invoice_id",
                table: "invoice_integrations_square",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_integrations_square_last_synced_at",
                table: "invoice_integrations_square",
                column: "last_synced_at");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_integrations_square_square_invoice_id",
                table: "invoice_integrations_square",
                column: "square_invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_integrations_square_sync_status_key",
                table: "invoice_integrations_square",
                column: "sync_status_key");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_invoice_id",
                table: "invoice_items",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_invoice_item_id",
                table: "invoice_items",
                column: "invoice_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_line_number",
                table: "invoice_items",
                column: "line_number");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_number_counters_prefix",
                table: "invoice_number_counters",
                column: "prefix");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_number_counters_updated_at",
                table: "invoice_number_counters",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_number_counters_year_2",
                table: "invoice_number_counters",
                column: "year_2");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_client_id",
                table: "invoices",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_created_at",
                table: "invoices",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_currency_code",
                table: "invoices",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_data_profile",
                table: "invoices",
                column: "data_profile");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_due_at",
                table: "invoices",
                column: "due_at");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_invoice_id",
                table: "invoices",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_invoice_number",
                table: "invoices",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_issued_at",
                table: "invoices",
                column: "issued_at");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_paid_at",
                table: "invoices",
                column: "paid_at");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_payment_method_key",
                table: "invoices",
                column: "payment_method_key");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_project_id",
                table: "invoices",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_status_key",
                table: "invoices",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_total_amount",
                table: "invoices",
                column: "total_amount");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_activity_id",
                table: "jobs",
                column: "activity_id");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_created_at",
                table: "jobs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_created_by_actor",
                table: "jobs",
                column: "created_by_actor");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_entity_key",
                table: "jobs",
                column: "entity_key");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_job_id",
                table: "jobs",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_job_type_key",
                table: "jobs",
                column: "job_type_key");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_locked_by",
                table: "jobs",
                column: "locked_by");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_locked_until",
                table: "jobs",
                column: "locked_until");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_priority_key",
                table: "jobs",
                column: "priority_key");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_run_after",
                table: "jobs",
                column: "run_after");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_status_key",
                table: "jobs",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_topic_key",
                table: "jobs",
                column: "topic_key");

            migrationBuilder.CreateIndex(
                name: "IX_leads_company_name",
                table: "leads",
                column: "company_name");

            migrationBuilder.CreateIndex(
                name: "IX_leads_contact_display_name",
                table: "leads",
                column: "contact_display_name");

            migrationBuilder.CreateIndex(
                name: "IX_leads_contact_email",
                table: "leads",
                column: "contact_email");

            migrationBuilder.CreateIndex(
                name: "IX_leads_contact_person_id",
                table: "leads",
                column: "contact_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_leads_converted_at",
                table: "leads",
                column: "converted_at");

            migrationBuilder.CreateIndex(
                name: "IX_leads_converted_client_id",
                table: "leads",
                column: "converted_client_id");

            migrationBuilder.CreateIndex(
                name: "IX_leads_converted_project_id",
                table: "leads",
                column: "converted_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_leads_created_at",
                table: "leads",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_leads_data_profile",
                table: "leads",
                column: "data_profile");

            migrationBuilder.CreateIndex(
                name: "IX_leads_interested_package_key",
                table: "leads",
                column: "interested_package_key");

            migrationBuilder.CreateIndex(
                name: "IX_leads_lead_id",
                table: "leads",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "IX_leads_outcome_key",
                table: "leads",
                column: "outcome_key");

            migrationBuilder.CreateIndex(
                name: "IX_leads_priority_key",
                table: "leads",
                column: "priority_key");

            migrationBuilder.CreateIndex(
                name: "IX_leads_source_key",
                table: "leads",
                column: "source_key");

            migrationBuilder.CreateIndex(
                name: "IX_leads_stage_key",
                table: "leads",
                column: "stage_key");

            migrationBuilder.CreateIndex(
                name: "IX_path_settings_default_project_root_key",
                table: "path_settings",
                column: "default_project_root_key");

            migrationBuilder.CreateIndex(
                name: "IX_path_settings_settings_id",
                table: "path_settings",
                column: "settings_id");

            migrationBuilder.CreateIndex(
                name: "IX_path_settings_updated_at",
                table: "path_settings",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "IX_path_templates_anchor_key",
                table: "path_templates",
                column: "anchor_key");

            migrationBuilder.CreateIndex(
                name: "IX_path_templates_created_at",
                table: "path_templates",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_path_templates_exists_required",
                table: "path_templates",
                column: "exists_required");

            migrationBuilder.CreateIndex(
                name: "IX_path_templates_is_active",
                table: "path_templates",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_path_templates_path_key",
                table: "path_templates",
                column: "path_key");

            migrationBuilder.CreateIndex(
                name: "IX_path_templates_path_type_key",
                table: "path_templates",
                column: "path_type_key");

            migrationBuilder.CreateIndex(
                name: "IX_payments_amount",
                table: "payments",
                column: "amount");

            migrationBuilder.CreateIndex(
                name: "IX_payments_captured_at",
                table: "payments",
                column: "captured_at");

            migrationBuilder.CreateIndex(
                name: "IX_payments_created_at",
                table: "payments",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_payments_currency_code",
                table: "payments",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "IX_payments_data_profile",
                table: "payments",
                column: "data_profile");

            migrationBuilder.CreateIndex(
                name: "IX_payments_invoice_id",
                table: "payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_method_key",
                table: "payments",
                column: "method_key");

            migrationBuilder.CreateIndex(
                name: "IX_payments_payment_id",
                table: "payments",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_processor_key",
                table: "payments",
                column: "processor_key");

            migrationBuilder.CreateIndex(
                name: "IX_payments_processor_payment_id",
                table: "payments",
                column: "processor_payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_recorded_by_person_id",
                table: "payments",
                column: "recorded_by_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_refunded_at",
                table: "payments",
                column: "refunded_at");

            migrationBuilder.CreateIndex(
                name: "IX_payments_status_key",
                table: "payments",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_people_created_at",
                table: "people",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_people_data_profile",
                table: "people",
                column: "data_profile");

            migrationBuilder.CreateIndex(
                name: "IX_people_default_host_key",
                table: "people",
                column: "default_host_key");

            migrationBuilder.CreateIndex(
                name: "IX_people_first_name",
                table: "people",
                column: "first_name");

            migrationBuilder.CreateIndex(
                name: "IX_people_last_name",
                table: "people",
                column: "last_name");

            migrationBuilder.CreateIndex(
                name: "IX_people_person_id",
                table: "people",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_people_status_key",
                table: "people",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_created_at",
                table: "permissions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_permission_key",
                table: "permissions",
                column: "permission_key");

            migrationBuilder.CreateIndex(
                name: "IX_person_calendar_sync_settings_person_id",
                table: "person_calendar_sync_settings",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_contacts_email",
                table: "person_contacts",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_person_contacts_person_id",
                table: "person_contacts",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_code_counters_prefix",
                table: "project_code_counters",
                column: "prefix");

            migrationBuilder.CreateIndex(
                name: "IX_project_code_counters_updated_at",
                table: "project_code_counters",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "IX_project_code_counters_year_2",
                table: "project_code_counters",
                column: "year_2");

            migrationBuilder.CreateIndex(
                name: "IX_project_storage_roots_created_at",
                table: "project_storage_roots",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_project_storage_roots_is_primary",
                table: "project_storage_roots",
                column: "is_primary");

            migrationBuilder.CreateIndex(
                name: "IX_project_storage_roots_project_id",
                table: "project_storage_roots",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_storage_roots_project_storage_root_id",
                table: "project_storage_roots",
                column: "project_storage_root_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_storage_roots_root_key",
                table: "project_storage_roots",
                column: "root_key");

            migrationBuilder.CreateIndex(
                name: "IX_project_storage_roots_storage_provider_key",
                table: "project_storage_roots",
                column: "storage_provider_key");

            migrationBuilder.CreateIndex(
                name: "IX_projects_client_id",
                table: "projects",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_created_at",
                table: "projects",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_projects_current_invoice_id",
                table: "projects",
                column: "current_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_data_profile",
                table: "projects",
                column: "data_profile");

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
                name: "IX_projects_project_id",
                table: "projects",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_status_key",
                table: "projects",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_slug_reservations_created_at",
                table: "slug_reservations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_slug_reservations_data_profile",
                table: "slug_reservations",
                column: "data_profile");

            migrationBuilder.CreateIndex(
                name: "IX_slug_reservations_expires_at",
                table: "slug_reservations",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_slug_reservations_reserved_at",
                table: "slug_reservations",
                column: "reserved_at");

            migrationBuilder.CreateIndex(
                name: "IX_slug_reservations_reserved_by_person_id",
                table: "slug_reservations",
                column: "reserved_by_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_slug_reservations_scope_key",
                table: "slug_reservations",
                column: "scope_key");

            migrationBuilder.CreateIndex(
                name: "IX_slug_reservations_slug",
                table: "slug_reservations",
                column: "slug");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_assigned_to_person_id",
                table: "work_items",
                column: "assigned_to_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_created_at",
                table: "work_items",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_data_profile",
                table: "work_items",
                column: "data_profile");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_due_at",
                table: "work_items",
                column: "due_at");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_priority_key",
                table: "work_items",
                column: "priority_key");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_project_id",
                table: "work_items",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_source_activity_id",
                table: "work_items",
                column: "source_activity_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_status_key",
                table: "work_items",
                column: "status_key");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_title",
                table: "work_items",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_work_item_id",
                table: "work_items",
                column: "work_item_id");

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_projects_project_id",
                table: "bookings",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "project_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_deliverable_versions_deliverables_deliverable_id",
                table: "deliverable_versions",
                column: "deliverable_id",
                principalTable: "deliverables",
                principalColumn: "deliverable_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_deliverables_invoices_invoice_id",
                table: "deliverables",
                column: "invoice_id",
                principalTable: "invoices",
                principalColumn: "invoice_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_deliverables_projects_project_id",
                table: "deliverables",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "project_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_integrations_square_invoices_invoice_id",
                table: "invoice_integrations_square",
                column: "invoice_id",
                principalTable: "invoices",
                principalColumn: "invoice_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_items_invoices_invoice_id",
                table: "invoice_items",
                column: "invoice_id",
                principalTable: "invoices",
                principalColumn: "invoice_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_projects_project_id",
                table: "invoices",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "project_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clients_people_account_owner_person_id",
                table: "clients");

            migrationBuilder.DropForeignKey(
                name: "FK_clients_people_primary_contact_person_id",
                table: "clients");

            migrationBuilder.DropForeignKey(
                name: "FK_deliverable_versions_people_approved_by_person_id",
                table: "deliverable_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_deliverable_versions_people_uploaded_by_person_id",
                table: "deliverable_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_deliverables_people_created_by_person_id",
                table: "deliverables");

            migrationBuilder.DropForeignKey(
                name: "FK_deliverables_projects_project_id",
                table: "deliverables");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_projects_project_id",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_clients_client_id",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_deliverable_versions_deliverables_deliverable_id",
                table: "deliverable_versions");

            migrationBuilder.DropTable(
                name: "activity_acknowledgements");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "client_billing_profiles");

            migrationBuilder.DropTable(
                name: "client_integrations_square");

            migrationBuilder.DropTable(
                name: "invoice_integrations_square");

            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropTable(
                name: "invoice_number_counters");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropTable(
                name: "leads");

            migrationBuilder.DropTable(
                name: "path_settings");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "person_calendar_sync_settings");

            migrationBuilder.DropTable(
                name: "person_contacts");

            migrationBuilder.DropTable(
                name: "project_code_counters");

            migrationBuilder.DropTable(
                name: "project_storage_roots");

            migrationBuilder.DropTable(
                name: "slug_reservations");

            migrationBuilder.DropTable(
                name: "work_items");

            migrationBuilder.DropTable(
                name: "path_templates");

            migrationBuilder.DropTable(
                name: "activity_log");

            migrationBuilder.DropTable(
                name: "people");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "deliverables");

            migrationBuilder.DropTable(
                name: "deliverable_versions");

            migrationBuilder.DropTable(
                name: "invoices");
        }
    }
}

