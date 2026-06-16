using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFlexibleAndWeeklyScheduleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TotalWeeklyHours",
                table: "WorkSchedules",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WorkingHours",
                table: "WorkScheduleDays",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalWeeklyHours",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "WorkingHours",
                table: "WorkScheduleDays");
        }
    }
}
