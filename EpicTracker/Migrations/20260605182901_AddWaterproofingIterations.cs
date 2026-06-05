using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EpicTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddWaterproofingIterations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WaterproofingIterations",
                table: "Epics",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WaterproofingIterations",
                table: "Epics");
        }
    }
}
