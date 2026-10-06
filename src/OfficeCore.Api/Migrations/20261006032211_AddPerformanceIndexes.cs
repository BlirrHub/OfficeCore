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
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Payslips_OrganizationId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_LeaveRequests_OrganizationId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_AttendanceRecords_OrganizationId\";");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Payslips_OrganizationId_PayrollPeriodId\" ON \"Payslips\" (\"OrganizationId\", \"PayrollPeriodId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_LeaveRequests_OrganizationId_EmployeeId_Status\" ON \"LeaveRequests\" (\"OrganizationId\", \"EmployeeId\", \"Status\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_AttendanceRecords_OrganizationId_EmployeeId_Timestamp\" ON \"AttendanceRecords\" (\"OrganizationId\", \"EmployeeId\", \"Timestamp\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_AttendanceRecords_OrganizationId_Timestamp\" ON \"AttendanceRecords\" (\"OrganizationId\", \"Timestamp\");");
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
