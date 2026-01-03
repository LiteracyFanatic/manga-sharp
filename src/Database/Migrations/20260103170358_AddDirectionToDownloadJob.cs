using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaSharp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectionToDownloadJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "DownloadJobs",
                type: "TEXT",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Direction",
                table: "DownloadJobs");
        }
    }
}
