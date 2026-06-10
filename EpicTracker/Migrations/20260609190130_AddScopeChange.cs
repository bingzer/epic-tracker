using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EpicTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddScopeChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScopeChange",
                table: "Specs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScopeChange",
                table: "Specs");
        }
    }
}
