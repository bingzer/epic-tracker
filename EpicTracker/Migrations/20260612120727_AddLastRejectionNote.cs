using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EpicTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddLastRejectionNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastRejectionNote",
                table: "Specs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRejectionNote",
                table: "Specs");
        }
    }
}
