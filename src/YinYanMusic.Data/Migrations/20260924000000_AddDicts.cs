using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace YinYanMusic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DictTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DictTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DictItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DictType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DictItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DictItems_DictType_IsEnabled_SortOrder",
                table: "DictItems",
                columns: new[] { "DictType", "IsEnabled", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DictItems_DictType_Label",
                table: "DictItems",
                columns: new[] { "DictType", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DictTypes_Type",
                table: "DictTypes",
                column: "Type",
                unique: true);

            // ── 种子数据（V2.16）────────────────────────────────────────────
            // 歌手地区/类型的预设选项，此前写死在 web/admin 的 Artists.vue 里，
            // 现在落到数据字典表，后台「数据字典」页可直接增删改，歌手编辑框实时生效。
            // ON CONFLICT 让迁移可重复应用（重复执行不会炸）。
            migrationBuilder.Sql(
                """
                INSERT INTO "DictTypes" ("Type", "Name", "IsEnabled", "CreatedAt")
                VALUES
                    ('artist_region', '歌手地区', true, now()),
                    ('artist_kind',   '歌手类型', true, now())
                ON CONFLICT ("Type") DO NOTHING;

                INSERT INTO "DictItems" ("DictType", "Label", "SortOrder", "IsEnabled", "CreatedAt")
                VALUES
                    ('artist_region', '华语',   1, true, now()),
                    ('artist_region', '欧美',   2, true, now()),
                    ('artist_region', '日韩',   3, true, now()),
                    ('artist_region', '粤语',   4, true, now()),
                    ('artist_region', '国风',   5, true, now()),
                    ('artist_region', '其他',   6, true, now()),
                    ('artist_kind',   '歌手',       1, true, now()),
                    ('artist_kind',   '乐队',       2, true, now()),
                    ('artist_kind',   '组合',       3, true, now()),
                    ('artist_kind',   '创作歌手',   4, true, now()),
                    ('artist_kind',   '说唱歌手',   5, true, now()),
                    ('artist_kind',   '偶像团体',   6, true, now())
                ON CONFLICT ("DictType", "Label") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DictItems");

            migrationBuilder.DropTable(
                name: "DictTypes");
        }
    }
}