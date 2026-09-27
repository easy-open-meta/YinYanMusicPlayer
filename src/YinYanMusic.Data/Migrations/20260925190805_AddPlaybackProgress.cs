using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YinYanMusic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaybackProgress",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SongId = table.Column<long>(type: "bigint", nullable: false),
                    PositionSeconds = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackProgress", x => new { x.UserId, x.SongId });
                    table.ForeignKey(
                        name: "FK_PlaybackProgress_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaybackProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackProgress_SongId",
                table: "PlaybackProgress",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackProgress_UserId_UpdatedAtUtc",
                table: "PlaybackProgress",
                columns: new[] { "UserId", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaybackProgress");
        }
    }
}
