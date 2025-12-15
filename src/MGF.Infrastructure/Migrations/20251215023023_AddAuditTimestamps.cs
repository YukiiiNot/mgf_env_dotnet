using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "projects",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "projects",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "people",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "people",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "clients",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "clients",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "people");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "people");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "clients");
        }
    }
}
