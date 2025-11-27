using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaSharp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "OrderIndex",
                table: "DownloadJobs",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderIndex",
                table: "DownloadJobs");
        }
    }
}
