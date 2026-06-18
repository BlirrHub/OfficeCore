using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateKanbanModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BoardCardId",
                table: "BoardCardLabels",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardLabels_BoardCardId",
                table: "BoardCardLabels",
                column: "BoardCardId");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardCardLabels_BoardCards_BoardCardId",
                table: "BoardCardLabels",
                column: "BoardCardId",
                principalTable: "BoardCards",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BoardCardLabels_BoardCards_BoardCardId",
                table: "BoardCardLabels");

            migrationBuilder.DropIndex(
                name: "IX_BoardCardLabels_BoardCardId",
                table: "BoardCardLabels");

            migrationBuilder.DropColumn(
                name: "BoardCardId",
                table: "BoardCardLabels");
        }
    }
}
