using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkScheduleTimeRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoNormalizeHalfDay",
                table: "WorkSchedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoSubmitOvertimeApproval",
                table: "WorkSchedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LateGracePeriodMinutes",
                table: "WorkSchedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OvertimeThresholdMinutes",
                table: "WorkSchedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoNormalizeHalfDay",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "AutoSubmitOvertimeApproval",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "LateGracePeriodMinutes",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "OvertimeThresholdMinutes",
                table: "WorkSchedules");
        }
    }
}
