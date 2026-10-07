using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyWorkScheduleOvertimePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkScheduleAutoDeductions");

            migrationBuilder.DropTable(
                name: "WorkScheduleOvertimeRules");

            migrationBuilder.AddColumn<bool>(
                name: "HasOvertimePolicy",
                table: "WorkSchedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "OvertimeMultiplier",
                table: "WorkSchedules",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasOvertimePolicy",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "OvertimeMultiplier",
                table: "WorkSchedules");

            migrationBuilder.CreateTable(
                name: "WorkScheduleAutoDeductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AfterWorkHours = table.Column<double>(type: "double precision", nullable: false),
                    DeductHours = table.Column<double>(type: "double precision", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkScheduleAutoDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkScheduleAutoDeductions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkScheduleAutoDeductions_WorkSchedules_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkScheduleOvertimeRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AfterHours = table.Column<double>(type: "double precision", nullable: true),
                    AfterTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Multiplier = table.Column<double>(type: "double precision", nullable: false),
                    TriggerMode = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkScheduleOvertimeRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkScheduleOvertimeRules_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkScheduleOvertimeRules_WorkSchedules_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleAutoDeductions_OrganizationId",
                table: "WorkScheduleAutoDeductions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleAutoDeductions_WorkScheduleId",
                table: "WorkScheduleAutoDeductions",
                column: "WorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleOvertimeRules_OrganizationId",
                table: "WorkScheduleOvertimeRules",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleOvertimeRules_WorkScheduleId",
                table: "WorkScheduleOvertimeRules",
                column: "WorkScheduleId");
        }
    }
}
