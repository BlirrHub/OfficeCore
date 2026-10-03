using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTinAndType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "Businesses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TinId",
                table: "Businesses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "TinId",
                table: "Businesses");
        }
    }
}
