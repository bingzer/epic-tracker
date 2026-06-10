using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EpicTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecLastKnownStateName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DependsOn",
                table: "Specs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastKnownStateName",
                table: "Specs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE Specs
                SET LastKnownStateName = CurrentStateName
                WHERE CurrentStateName NOT IN ('spec_human_in_loop', 'agent_swarm')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DependsOn",
                table: "Specs");

            migrationBuilder.DropColumn(
                name: "LastKnownStateName",
                table: "Specs");
        }
    }
}
