using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeductionMultiplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeductionMultiplier",
                table: "PayrollPeriods",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeductionMultiplier",
                table: "PayrollPeriods");
        }
    }
}
