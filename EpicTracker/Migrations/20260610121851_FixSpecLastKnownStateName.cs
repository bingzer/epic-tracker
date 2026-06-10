using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EpicTracker.Migrations
{
    /// <inheritdoc />
    public partial class FixSpecLastKnownStateName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix specs where LastKnownStateName was incorrectly set to a transient state.
            // spec_human_in_loop always follows ac, so fall back based on IsAcPassed.
            migrationBuilder.Sql("""
                UPDATE Specs
                SET LastKnownStateName = CASE
                    WHEN IsAcPassed IS NOT NULL THEN 'ac'
                    WHEN IsCodeDone = 1 THEN 'coding'
                    ELSE 'ready'
                END
                WHERE LastKnownStateName IN ('spec_human_in_loop', 'agent_swarm')
                   OR (CurrentStateName IN ('spec_human_in_loop', 'agent_swarm') AND LastKnownStateName IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
