using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YinYanMusic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentThreading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Depth",
                table: "Comments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Comments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "RootId",
                table: "Comments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_RootId_Id",
                table: "Comments",
                columns: new[] { "RootId", "Id" });

            // 存量回填：V2.9 初版的回复只可能有一层，所以那条回复的 Depth 就是 1、RootId 就是它的父评论。
            // 不回填的话，老回复的 Depth 会停在默认值 0（客户端按它算缩进 → 老回复不缩进）、
            // RootId 为 null（子树查询直接查不到它们，列表里老回复会整批消失）。
            migrationBuilder.Sql("""
                UPDATE "Comments" SET "Depth" = 1, "RootId" = "ParentId" WHERE "ParentId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comments_RootId_Id",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "Depth",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "RootId",
                table: "Comments");
        }
    }
}
