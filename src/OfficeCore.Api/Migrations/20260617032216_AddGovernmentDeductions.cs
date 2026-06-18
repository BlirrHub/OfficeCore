using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernmentDeductions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeGovernmentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    IsEligibleForDeductions = table.Column<bool>(type: "boolean", nullable: false),
                    SssNumber = table.Column<string>(type: "text", nullable: true),
                    PagIbigNumber = table.Column<string>(type: "text", nullable: true),
                    PhilHealthNumber = table.Column<string>(type: "text", nullable: true),
                    TinNumber = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeGovernmentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeGovernmentRecords_AspNetUsers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GovernmentDeductionSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgencyName = table.Column<string>(type: "text", nullable: false),
                    EmployeeShare = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerShare = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentDeductionSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGovernmentRecords_EmployeeId",
                table: "EmployeeGovernmentRecords",
                column: "EmployeeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeGovernmentRecords");

            migrationBuilder.DropTable(
                name: "GovernmentDeductionSettings");
        }
    }
}
