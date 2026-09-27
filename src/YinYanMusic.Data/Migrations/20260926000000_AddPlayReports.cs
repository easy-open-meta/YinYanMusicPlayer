using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YinYanMusic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayReports",
                columns: table => new
                {
                    ClientReportKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SongId = table.Column<long>(type: "bigint", nullable: false),
                    PlayedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayReports", x => x.ClientReportKey);
                    table.ForeignKey(
                        name: "FK_PlayReports_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayReports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayReports_SongId",
                table: "PlayReports",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayReports_UserId_PlayedAtUtc",
                table: "PlayReports",
                columns: new[] { "UserId", "PlayedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayReports");
        }
    }
}
