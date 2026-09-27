using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YinYanMusic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbumArtists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlbumArtists",
                columns: table => new
                {
                    AlbumId = table.Column<long>(type: "bigint", nullable: false),
                    ArtistId = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumArtists", x => new { x.AlbumId, x.ArtistId });
                    table.ForeignKey(
                        name: "FK_AlbumArtists_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumArtists_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumArtists_ArtistId",
                table: "AlbumArtists",
                column: "ArtistId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlbumArtists");
        }
    }
}
