using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkSchedulesAndDailyRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DailyRate",
                table: "AspNetUsers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkScheduleId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WorkArrangement = table.Column<int>(type: "integer", nullable: false),
                    IncludeTimeTrackedBeforeScheduledStartTime = table.Column<bool>(type: "boolean", nullable: false),
                    SplitTimesheetsAt = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkScheduleDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    IsRestDay = table.Column<bool>(type: "boolean", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkScheduleDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkScheduleDays_WorkSchedules_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_WorkScheduleId",
                table: "AspNetUsers",
                column: "WorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkScheduleDays_WorkScheduleId",
                table: "WorkScheduleDays",
                column: "WorkScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_WorkSchedules_WorkScheduleId",
                table: "AspNetUsers",
                column: "WorkScheduleId",
                principalTable: "WorkSchedules",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_WorkSchedules_WorkScheduleId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "WorkScheduleDays");

            migrationBuilder.DropTable(
                name: "WorkSchedules");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_WorkScheduleId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DailyRate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WorkScheduleId",
                table: "AspNetUsers");
        }
    }
}
