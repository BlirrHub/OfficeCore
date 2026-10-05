using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixBoardCardAndChatRoomMemberCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BoardCards_Employees_AssignedToUserId",
                table: "BoardCards");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRoomMembers_Employees_UserId",
                table: "ChatRoomMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCards_Employees_AssignedToUserId",
                table: "BoardCards",
                column: "AssignedToUserId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRoomMembers_Employees_UserId",
                table: "ChatRoomMembers",
                column: "UserId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BoardCards_Employees_AssignedToUserId",
                table: "BoardCards");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRoomMembers_Employees_UserId",
                table: "ChatRoomMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCards_Employees_AssignedToUserId",
                table: "BoardCards",
                column: "AssignedToUserId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRoomMembers_Employees_UserId",
                table: "ChatRoomMembers",
                column: "UserId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
