using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaSharp.Database.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProgressStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "DownloadJobs");

            migrationBuilder.RenameColumn(
                name: "ProgressMessage",
                table: "DownloadJobs",
                newName: "ProgressChapterTitle");

            migrationBuilder.AddColumn<int>(
                name: "ProgressChapterIndex",
                table: "DownloadJobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPageIndex",
                table: "DownloadJobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressTotalChapters",
                table: "DownloadJobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressTotalPages",
                table: "DownloadJobs",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgressChapterIndex",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "ProgressPageIndex",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "ProgressTotalChapters",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "ProgressTotalPages",
                table: "DownloadJobs");

            migrationBuilder.RenameColumn(
                name: "ProgressChapterTitle",
                table: "DownloadJobs",
                newName: "ProgressMessage");

            migrationBuilder.AddColumn<double>(
                name: "ProgressPercent",
                table: "DownloadJobs",
                type: "REAL",
                nullable: true);
        }
    }
}
