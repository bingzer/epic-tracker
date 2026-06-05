using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EpicTracker.Migrations
{
    /// <inheritdoc />
    public partial class RenameAgentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MockupPath",
                table: "Epics");

            migrationBuilder.RenameColumn(
                name: "ReviewerAgentId",
                table: "Specs",
                newName: "ReviewerAgentName");

            migrationBuilder.RenameColumn(
                name: "ReviewerAgentId",
                table: "Epics",
                newName: "ReviewerAgentName");

            migrationBuilder.RenameColumn(
                name: "EpicAgent",
                table: "Epics",
                newName: "EpicAgentName");

            migrationBuilder.RenameColumn(
                name: "CodingAgents",
                table: "Epics",
                newName: "CodingAgentNames");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReviewerAgentName",
                table: "Specs",
                newName: "ReviewerAgentId");

            migrationBuilder.RenameColumn(
                name: "ReviewerAgentName",
                table: "Epics",
                newName: "ReviewerAgentId");

            migrationBuilder.RenameColumn(
                name: "EpicAgentName",
                table: "Epics",
                newName: "EpicAgent");

            migrationBuilder.RenameColumn(
                name: "CodingAgentNames",
                table: "Epics",
                newName: "CodingAgents");

            migrationBuilder.AddColumn<string>(
                name: "MockupPath",
                table: "Epics",
                type: "TEXT",
                nullable: true);
        }
    }
}
