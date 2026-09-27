using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YinYanMusic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationAlbum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RelatedAlbumId",
                table: "Notifications",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedAlbumId",
                table: "Notifications");
        }
    }
}
