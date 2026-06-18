using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkScheduleRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkScheduleAutoDeductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeductHours = table.Column<double>(type: "double precision", nullable: false),
                    AfterWorkHours = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkScheduleAutoDeductions", x => x.Id);
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
                    WorkScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerMode = table.Column<int>(type: "integer", nullable: false),
                    AfterHours = table.Column<double>(type: "double precision", nullable: true),
                    AfterTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Multiplier = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkScheduleOvertimeRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkScheduleOvertimeRules_WorkSchedules_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleAutoDeductions_WorkScheduleId",
                table: "WorkScheduleAutoDeductions",
                column: "WorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleOvertimeRules_WorkScheduleId",
                table: "WorkScheduleOvertimeRules",
                column: "WorkScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkScheduleAutoDeductions");

            migrationBuilder.DropTable(
                name: "WorkScheduleOvertimeRules");
        }
    }
}
