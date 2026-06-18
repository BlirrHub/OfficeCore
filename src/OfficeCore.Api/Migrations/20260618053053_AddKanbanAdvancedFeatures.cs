using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKanbanAdvancedFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoardCardAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardCardAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardCardAttachments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardCardAttachments_BoardCards_CardId",
                        column: x => x.CardId,
                        principalTable: "BoardCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardCardChecklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardCardChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardCardChecklists_BoardCards_CardId",
                        column: x => x.CardId,
                        principalTable: "BoardCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardCardComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardCardComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardCardComments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardCardComments_BoardCards_CardId",
                        column: x => x.CardId,
                        principalTable: "BoardCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardLabels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardLabels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardLabels_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardCardChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardCardChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardCardChecklistItems_BoardCardChecklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalTable: "BoardCardChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardCardLabels",
                columns: table => new
                {
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabelId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardCardLabels", x => new { x.CardId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_BoardCardLabels_BoardCards_CardId",
                        column: x => x.CardId,
                        principalTable: "BoardCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardCardLabels_BoardLabels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "BoardLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardAttachments_CardId",
                table: "BoardCardAttachments",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardAttachments_UserId",
                table: "BoardCardAttachments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardChecklistItems_ChecklistId",
                table: "BoardCardChecklistItems",
                column: "ChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardChecklists_CardId",
                table: "BoardCardChecklists",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardComments_CardId",
                table: "BoardCardComments",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardComments_UserId",
                table: "BoardCardComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardLabels_LabelId",
                table: "BoardCardLabels",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardLabels_BoardId",
                table: "BoardLabels",
                column: "BoardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardCardAttachments");

            migrationBuilder.DropTable(
                name: "BoardCardChecklistItems");

            migrationBuilder.DropTable(
                name: "BoardCardComments");

            migrationBuilder.DropTable(
                name: "BoardCardLabels");

            migrationBuilder.DropTable(
                name: "BoardCardChecklists");

            migrationBuilder.DropTable(
                name: "BoardLabels");
        }
    }
}
