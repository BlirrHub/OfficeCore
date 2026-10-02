using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardListColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "BoardLists",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorHex",
                table: "BoardLists");
        }
    }
}
