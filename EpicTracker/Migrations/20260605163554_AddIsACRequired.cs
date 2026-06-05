using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EpicTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddIsACRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeReviewRequired",
                table: "Specs");

            migrationBuilder.AddColumn<bool>(
                name: "IsACRequired",
                table: "Specs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCodeReviewRequired",
                table: "Specs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsACRequired",
                table: "Epics",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCodeReviewRequired",
                table: "Epics",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsACRequired",
                table: "Specs");

            migrationBuilder.DropColumn(
                name: "IsCodeReviewRequired",
                table: "Specs");

            migrationBuilder.DropColumn(
                name: "IsACRequired",
                table: "Epics");

            migrationBuilder.DropColumn(
                name: "IsCodeReviewRequired",
                table: "Epics");

            migrationBuilder.AddColumn<bool>(
                name: "CodeReviewRequired",
                table: "Specs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
