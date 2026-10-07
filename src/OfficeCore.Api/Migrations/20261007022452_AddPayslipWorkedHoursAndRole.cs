using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPayslipWorkedHoursAndRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedRole",
                table: "Payslips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TotalWorkedHours",
                table: "Payslips",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedRole",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "TotalWorkedHours",
                table: "Payslips");
        }
    }
}
