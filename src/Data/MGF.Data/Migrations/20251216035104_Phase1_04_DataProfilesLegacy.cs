using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MGF.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_04_DataProfilesLegacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_profiles_0",
                table: "data_profiles");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "data_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_profiles_0",
                table: "data_profiles",
                sql: "profile_key IN ('real','dummy','fixture','legacy')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_profiles_0",
                table: "data_profiles");

            migrationBuilder.DropColumn(
                name: "description",
                table: "data_profiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_profiles_0",
                table: "data_profiles",
                sql: "profile_key IN ('real','dummy','fixture')");
        }
    }
}

