using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class TimeTrackedPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CutoffDate",
                table: "PayrollPeriods");

            migrationBuilder.AddColumn<bool>(
                name: "AutoClockOut",
                table: "WorkSchedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoClockOut",
                table: "WorkSchedules");

            migrationBuilder.AddColumn<DateOnly>(
                name: "CutoffDate",
                table: "PayrollPeriods",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));
        }
    }
}
