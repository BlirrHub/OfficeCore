using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payslips_OrganizationId",
                table: "Payslips");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_OrganizationId",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_OrganizationId",
                table: "AttendanceRecords");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_OrganizationId_PayrollPeriodId",
                table: "Payslips",
                columns: new[] { "OrganizationId", "PayrollPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_OrganizationId_EmployeeId_Status",
                table: "LeaveRequests",
                columns: new[] { "OrganizationId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_OrganizationId_EmployeeId_Timestamp",
                table: "AttendanceRecords",
                columns: new[] { "OrganizationId", "EmployeeId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_OrganizationId_Timestamp",
                table: "AttendanceRecords",
                columns: new[] { "OrganizationId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payslips_OrganizationId_PayrollPeriodId",
                table: "Payslips");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_OrganizationId_EmployeeId_Status",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_OrganizationId_EmployeeId_Timestamp",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_OrganizationId_Timestamp",
                table: "AttendanceRecords");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_OrganizationId",
                table: "Payslips",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_OrganizationId",
                table: "LeaveRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_OrganizationId",
                table: "AttendanceRecords",
                column: "OrganizationId");
        }
    }
}
