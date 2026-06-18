using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenantRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdvanceRequests_AspNetUsers_EmployeeId",
                table: "AdvanceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_WorkSchedules_WorkScheduleId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_AspNetUsers_EmployeeId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCardAttachments_AspNetUsers_UserId",
                table: "BoardCardAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCardComments_AspNetUsers_UserId",
                table: "BoardCardComments");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCards_AspNetUsers_AssignedToUserId",
                table: "BoardCards");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardMembers_AspNetUsers_UserId",
                table: "BoardMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Boards_AspNetUsers_CreatedById",
                table: "Boards");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_AspNetUsers_SenderId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRoomMembers_AspNetUsers_UserId",
                table: "ChatRoomMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeFingerprints_AspNetUsers_EmployeeId",
                table: "EmployeeFingerprints");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeGovernmentRecords_AspNetUsers_EmployeeId",
                table: "EmployeeGovernmentRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_AspNetUsers_EmployeeId",
                table: "LeaveRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Payslips_AspNetUsers_EmployeeId",
                table: "Payslips");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_WorkScheduleId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DailyRate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DateHired",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsEligibleForLeave",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UseCustomDailyRate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WorkScheduleId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkSchedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkScheduleOvertimeRules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkScheduleDays",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkScheduleBreaks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkScheduleAutoDeductions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Payslips",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "PayrollPeriods",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "LeaveRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Holidays",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "HolidayCalendars",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "GovernmentDeductionSettings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "GeneralPayrollSettings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "EmployeeGovernmentRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "EmployeeFingerprints",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CompanyExpenses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Clients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ChatRooms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ChatRoomMembers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ChatMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Businesses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Boards",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BoardMembers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BoardLists",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BoardLabels",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BoardCards",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BoardCardLabels",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BoardCardComments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BoardCardChecklists",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BoardCardChecklistItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "BoardCardAttachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AttendanceRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AdvanceRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Organizations_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsEmployee = table.Column<bool>(type: "boolean", nullable: false),
                    DailyRate = table.Column<decimal>(type: "numeric", nullable: true),
                    UseCustomDailyRate = table.Column<bool>(type: "boolean", nullable: false),
                    DateHired = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEligibleForLeave = table.Column<bool>(type: "boolean", nullable: false),
                    WorkScheduleId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Employees_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Employees_WorkSchedules_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalTable: "WorkSchedules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_OrganizationId",
                table: "WorkSchedules",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleOvertimeRules_OrganizationId",
                table: "WorkScheduleOvertimeRules",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleDays_OrganizationId",
                table: "WorkScheduleDays",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleBreaks_OrganizationId",
                table: "WorkScheduleBreaks",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleAutoDeductions_OrganizationId",
                table: "WorkScheduleAutoDeductions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_OrganizationId",
                table: "Payslips",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_OrganizationId",
                table: "PayrollPeriods",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_OrganizationId",
                table: "LeaveRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_OrganizationId",
                table: "Holidays",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_OrganizationId",
                table: "HolidayCalendars",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentDeductionSettings_OrganizationId",
                table: "GovernmentDeductionSettings",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralPayrollSettings_OrganizationId",
                table: "GeneralPayrollSettings",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGovernmentRecords_OrganizationId",
                table: "EmployeeGovernmentRecords",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFingerprints_OrganizationId",
                table: "EmployeeFingerprints",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyExpenses_OrganizationId",
                table: "CompanyExpenses",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_OrganizationId",
                table: "Clients",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_OrganizationId",
                table: "ChatRooms",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_OrganizationId",
                table: "ChatRoomMembers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_OrganizationId",
                table: "ChatMessages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_OrganizationId",
                table: "Businesses",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_OrganizationId",
                table: "Boards",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardMembers_OrganizationId",
                table: "BoardMembers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardLists_OrganizationId",
                table: "BoardLists",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardLabels_OrganizationId",
                table: "BoardLabels",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCards_OrganizationId",
                table: "BoardCards",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardLabels_OrganizationId",
                table: "BoardCardLabels",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardComments_OrganizationId",
                table: "BoardCardComments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardChecklists_OrganizationId",
                table: "BoardCardChecklists",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardChecklistItems_OrganizationId",
                table: "BoardCardChecklistItems",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardAttachments_OrganizationId",
                table: "BoardCardAttachments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_OrganizationId",
                table: "AttendanceRecords",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvanceRequests_OrganizationId",
                table: "AdvanceRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_OrganizationId",
                table: "Employees",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_WorkScheduleId",
                table: "Employees",
                column: "WorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_CreatedById",
                table: "Organizations",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_AdvanceRequests_Employees_EmployeeId",
                table: "AdvanceRequests",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AdvanceRequests_Organizations_OrganizationId",
                table: "AdvanceRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_Employees_EmployeeId",
                table: "AttendanceRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_Organizations_OrganizationId",
                table: "AttendanceRecords",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCardAttachments_Employees_UserId",
                table: "BoardCardAttachments",
                column: "UserId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCardAttachments_Organizations_OrganizationId",
                table: "BoardCardAttachments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCardChecklistItems_Organizations_OrganizationId",
                table: "BoardCardChecklistItems",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCardChecklists_Organizations_OrganizationId",
                table: "BoardCardChecklists",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCardComments_Employees_UserId",
                table: "BoardCardComments",
                column: "UserId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCardComments_Organizations_OrganizationId",
                table: "BoardCardComments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCardLabels_Organizations_OrganizationId",
                table: "BoardCardLabels",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCards_Employees_AssignedToUserId",
                table: "BoardCards",
                column: "AssignedToUserId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCards_Organizations_OrganizationId",
                table: "BoardCards",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardLabels_Organizations_OrganizationId",
                table: "BoardLabels",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardLists_Organizations_OrganizationId",
                table: "BoardLists",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardMembers_Employees_UserId",
                table: "BoardMembers",
                column: "UserId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardMembers_Organizations_OrganizationId",
                table: "BoardMembers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Employees_CreatedById",
                table: "Boards",
                column: "CreatedById",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Organizations_OrganizationId",
                table: "Boards",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Businesses_Organizations_OrganizationId",
                table: "Businesses",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Employees_SenderId",
                table: "ChatMessages",
                column: "SenderId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Organizations_OrganizationId",
                table: "ChatMessages",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRoomMembers_Employees_UserId",
                table: "ChatRoomMembers",
                column: "UserId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRoomMembers_Organizations_OrganizationId",
                table: "ChatRoomMembers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_Organizations_OrganizationId",
                table: "ChatRooms",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Organizations_OrganizationId",
                table: "Clients",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyExpenses_Organizations_OrganizationId",
                table: "CompanyExpenses",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeFingerprints_Employees_EmployeeId",
                table: "EmployeeFingerprints",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeFingerprints_Organizations_OrganizationId",
                table: "EmployeeFingerprints",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeGovernmentRecords_Employees_EmployeeId",
                table: "EmployeeGovernmentRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeGovernmentRecords_Organizations_OrganizationId",
                table: "EmployeeGovernmentRecords",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GeneralPayrollSettings_Organizations_OrganizationId",
                table: "GeneralPayrollSettings",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GovernmentDeductionSettings_Organizations_OrganizationId",
                table: "GovernmentDeductionSettings",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HolidayCalendars_Organizations_OrganizationId",
                table: "HolidayCalendars",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Holidays_Organizations_OrganizationId",
                table: "Holidays",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_Employees_EmployeeId",
                table: "LeaveRequests",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_Organizations_OrganizationId",
                table: "LeaveRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollPeriods_Organizations_OrganizationId",
                table: "PayrollPeriods",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payslips_Employees_EmployeeId",
                table: "Payslips",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payslips_Organizations_OrganizationId",
                table: "Payslips",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkScheduleAutoDeductions_Organizations_OrganizationId",
                table: "WorkScheduleAutoDeductions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkScheduleBreaks_Organizations_OrganizationId",
                table: "WorkScheduleBreaks",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkScheduleDays_Organizations_OrganizationId",
                table: "WorkScheduleDays",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkScheduleOvertimeRules_Organizations_OrganizationId",
                table: "WorkScheduleOvertimeRules",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSchedules_Organizations_OrganizationId",
                table: "WorkSchedules",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdvanceRequests_Employees_EmployeeId",
                table: "AdvanceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_AdvanceRequests_Organizations_OrganizationId",
                table: "AdvanceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_Employees_EmployeeId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_Organizations_OrganizationId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCardAttachments_Employees_UserId",
                table: "BoardCardAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCardAttachments_Organizations_OrganizationId",
                table: "BoardCardAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCardChecklistItems_Organizations_OrganizationId",
                table: "BoardCardChecklistItems");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCardChecklists_Organizations_OrganizationId",
                table: "BoardCardChecklists");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCardComments_Employees_UserId",
                table: "BoardCardComments");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCardComments_Organizations_OrganizationId",
                table: "BoardCardComments");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCardLabels_Organizations_OrganizationId",
                table: "BoardCardLabels");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCards_Employees_AssignedToUserId",
                table: "BoardCards");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardCards_Organizations_OrganizationId",
                table: "BoardCards");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardLabels_Organizations_OrganizationId",
                table: "BoardLabels");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardLists_Organizations_OrganizationId",
                table: "BoardLists");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardMembers_Employees_UserId",
                table: "BoardMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardMembers_Organizations_OrganizationId",
                table: "BoardMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Boards_Employees_CreatedById",
                table: "Boards");

            migrationBuilder.DropForeignKey(
                name: "FK_Boards_Organizations_OrganizationId",
                table: "Boards");

            migrationBuilder.DropForeignKey(
                name: "FK_Businesses_Organizations_OrganizationId",
                table: "Businesses");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Employees_SenderId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Organizations_OrganizationId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRoomMembers_Employees_UserId",
                table: "ChatRoomMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRoomMembers_Organizations_OrganizationId",
                table: "ChatRoomMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_Organizations_OrganizationId",
                table: "ChatRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Organizations_OrganizationId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyExpenses_Organizations_OrganizationId",
                table: "CompanyExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeFingerprints_Employees_EmployeeId",
                table: "EmployeeFingerprints");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeFingerprints_Organizations_OrganizationId",
                table: "EmployeeFingerprints");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeGovernmentRecords_Employees_EmployeeId",
                table: "EmployeeGovernmentRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeGovernmentRecords_Organizations_OrganizationId",
                table: "EmployeeGovernmentRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_GeneralPayrollSettings_Organizations_OrganizationId",
                table: "GeneralPayrollSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_GovernmentDeductionSettings_Organizations_OrganizationId",
                table: "GovernmentDeductionSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_HolidayCalendars_Organizations_OrganizationId",
                table: "HolidayCalendars");

            migrationBuilder.DropForeignKey(
                name: "FK_Holidays_Organizations_OrganizationId",
                table: "Holidays");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_Employees_EmployeeId",
                table: "LeaveRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_Organizations_OrganizationId",
                table: "LeaveRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollPeriods_Organizations_OrganizationId",
                table: "PayrollPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_Payslips_Employees_EmployeeId",
                table: "Payslips");

            migrationBuilder.DropForeignKey(
                name: "FK_Payslips_Organizations_OrganizationId",
                table: "Payslips");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkScheduleAutoDeductions_Organizations_OrganizationId",
                table: "WorkScheduleAutoDeductions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkScheduleBreaks_Organizations_OrganizationId",
                table: "WorkScheduleBreaks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkScheduleDays_Organizations_OrganizationId",
                table: "WorkScheduleDays");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkScheduleOvertimeRules_Organizations_OrganizationId",
                table: "WorkScheduleOvertimeRules");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSchedules_Organizations_OrganizationId",
                table: "WorkSchedules");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_WorkSchedules_OrganizationId",
                table: "WorkSchedules");

            migrationBuilder.DropIndex(
                name: "IX_WorkScheduleOvertimeRules_OrganizationId",
                table: "WorkScheduleOvertimeRules");

            migrationBuilder.DropIndex(
                name: "IX_WorkScheduleDays_OrganizationId",
                table: "WorkScheduleDays");

            migrationBuilder.DropIndex(
                name: "IX_WorkScheduleBreaks_OrganizationId",
                table: "WorkScheduleBreaks");

            migrationBuilder.DropIndex(
                name: "IX_WorkScheduleAutoDeductions_OrganizationId",
                table: "WorkScheduleAutoDeductions");

            migrationBuilder.DropIndex(
                name: "IX_Payslips_OrganizationId",
                table: "Payslips");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPeriods_OrganizationId",
                table: "PayrollPeriods");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_OrganizationId",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_Holidays_OrganizationId",
                table: "Holidays");

            migrationBuilder.DropIndex(
                name: "IX_HolidayCalendars_OrganizationId",
                table: "HolidayCalendars");

            migrationBuilder.DropIndex(
                name: "IX_GovernmentDeductionSettings_OrganizationId",
                table: "GovernmentDeductionSettings");

            migrationBuilder.DropIndex(
                name: "IX_GeneralPayrollSettings_OrganizationId",
                table: "GeneralPayrollSettings");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeGovernmentRecords_OrganizationId",
                table: "EmployeeGovernmentRecords");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeFingerprints_OrganizationId",
                table: "EmployeeFingerprints");

            migrationBuilder.DropIndex(
                name: "IX_CompanyExpenses_OrganizationId",
                table: "CompanyExpenses");

            migrationBuilder.DropIndex(
                name: "IX_Clients_OrganizationId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_ChatRooms_OrganizationId",
                table: "ChatRooms");

            migrationBuilder.DropIndex(
                name: "IX_ChatRoomMembers_OrganizationId",
                table: "ChatRoomMembers");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_OrganizationId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_Businesses_OrganizationId",
                table: "Businesses");

            migrationBuilder.DropIndex(
                name: "IX_Boards_OrganizationId",
                table: "Boards");

            migrationBuilder.DropIndex(
                name: "IX_BoardMembers_OrganizationId",
                table: "BoardMembers");

            migrationBuilder.DropIndex(
                name: "IX_BoardLists_OrganizationId",
                table: "BoardLists");

            migrationBuilder.DropIndex(
                name: "IX_BoardLabels_OrganizationId",
                table: "BoardLabels");

            migrationBuilder.DropIndex(
                name: "IX_BoardCards_OrganizationId",
                table: "BoardCards");

            migrationBuilder.DropIndex(
                name: "IX_BoardCardLabels_OrganizationId",
                table: "BoardCardLabels");

            migrationBuilder.DropIndex(
                name: "IX_BoardCardComments_OrganizationId",
                table: "BoardCardComments");

            migrationBuilder.DropIndex(
                name: "IX_BoardCardChecklists_OrganizationId",
                table: "BoardCardChecklists");

            migrationBuilder.DropIndex(
                name: "IX_BoardCardChecklistItems_OrganizationId",
                table: "BoardCardChecklistItems");

            migrationBuilder.DropIndex(
                name: "IX_BoardCardAttachments_OrganizationId",
                table: "BoardCardAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_OrganizationId",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AdvanceRequests_OrganizationId",
                table: "AdvanceRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkScheduleOvertimeRules");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkScheduleDays");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkScheduleBreaks");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkScheduleAutoDeductions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "HolidayCalendars");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "GovernmentDeductionSettings");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "GeneralPayrollSettings");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "EmployeeGovernmentRecords");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "EmployeeFingerprints");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CompanyExpenses");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ChatRoomMembers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Boards");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BoardMembers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BoardLists");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BoardLabels");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BoardCards");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BoardCardLabels");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BoardCardComments");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BoardCardChecklists");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BoardCardChecklistItems");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "BoardCardAttachments");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AdvanceRequests");

            migrationBuilder.AddColumn<decimal>(
                name: "DailyRate",
                table: "AspNetUsers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateHired",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeId",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEligibleForLeave",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseCustomDailyRate",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkScheduleId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_WorkScheduleId",
                table: "AspNetUsers",
                column: "WorkScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdvanceRequests_AspNetUsers_EmployeeId",
                table: "AdvanceRequests",
                column: "EmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_WorkSchedules_WorkScheduleId",
                table: "AspNetUsers",
                column: "WorkScheduleId",
                principalTable: "WorkSchedules",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_AspNetUsers_EmployeeId",
                table: "AttendanceRecords",
                column: "EmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCardAttachments_AspNetUsers_UserId",
                table: "BoardCardAttachments",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCardComments_AspNetUsers_UserId",
                table: "BoardCardComments",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCards_AspNetUsers_AssignedToUserId",
                table: "BoardCards",
                column: "AssignedToUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardMembers_AspNetUsers_UserId",
                table: "BoardMembers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_AspNetUsers_CreatedById",
                table: "Boards",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_AspNetUsers_SenderId",
                table: "ChatMessages",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRoomMembers_AspNetUsers_UserId",
                table: "ChatRoomMembers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeFingerprints_AspNetUsers_EmployeeId",
                table: "EmployeeFingerprints",
                column: "EmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeGovernmentRecords_AspNetUsers_EmployeeId",
                table: "EmployeeGovernmentRecords",
                column: "EmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_AspNetUsers_EmployeeId",
                table: "LeaveRequests",
                column: "EmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payslips_AspNetUsers_EmployeeId",
                table: "Payslips",
                column: "EmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
