using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YinYanMusic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSongArtists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SongArtists",
                columns: table => new
                {
                    SongId = table.Column<long>(type: "bigint", nullable: false),
                    ArtistId = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongArtists", x => new { x.SongId, x.ArtistId });
                    table.ForeignKey(
                        name: "FK_SongArtists_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SongArtists_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SongArtists_ArtistId",
                table: "SongArtists",
                column: "ArtistId");

            // ── 存量回填（手写，EF 生成不了）────────────────────────────────────────
            // 每首现有歌都挂上它的主歌手（Position = 0），保证"按歌手列歌"改走关联表之后
            // 老数据一首都不会丢。ON CONFLICT 让这段可重复执行（重复应用迁移也不会炸）。
            migrationBuilder.Sql(
                """
                INSERT INTO "SongArtists" ("SongId", "ArtistId", "Position")
                SELECT s."Id", s."ArtistId", 0 FROM "Songs" AS s
                ON CONFLICT ("SongId", "ArtistId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SongArtists");
        }
    }
}
