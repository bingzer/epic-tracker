using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EpicTracker.Migrations
{
    /// <inheritdoc />
    public partial class RestoreTypedEpicFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Epics",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    EpicAgent = table.Column<string>(type: "TEXT", nullable: false),
                    Brief = table.Column<string>(type: "TEXT", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentStateName = table.Column<string>(type: "TEXT", nullable: false),
                    CodingAgents = table.Column<string>(type: "TEXT", nullable: false),
                    AgentSwarm = table.Column<string>(type: "TEXT", nullable: true),
                    HumanInLoop = table.Column<string>(type: "TEXT", nullable: true),
                    IsDocDrafted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMockupDone = table.Column<bool>(type: "INTEGER", nullable: false),
                    MockupPath = table.Column<string>(type: "TEXT", nullable: true),
                    NeedsMockup = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Epics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EpicAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EpicId = table.Column<string>(type: "TEXT", nullable: false),
                    EpicAgentId = table.Column<string>(type: "TEXT", nullable: false),
                    EpicAgentInstruction = table.Column<string>(type: "TEXT", nullable: true),
                    FromState = table.Column<string>(type: "TEXT", nullable: false),
                    ToState = table.Column<string>(type: "TEXT", nullable: false),
                    AgentSwarm = table.Column<string>(type: "TEXT", nullable: true),
                    HumanInLoop = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpicAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpicAudits_Epics_EpicId",
                        column: x => x.EpicId,
                        principalTable: "Epics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Specs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EpicId = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentStateName = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedAgentId = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewerAgentId = table.Column<string>(type: "TEXT", nullable: true),
                    EpicAgentInstruction = table.Column<string>(type: "TEXT", nullable: true),
                    SpecDocPath = table.Column<string>(type: "TEXT", nullable: true),
                    AgentSwarm = table.Column<string>(type: "TEXT", nullable: true),
                    HumanInLoop = table.Column<string>(type: "TEXT", nullable: true),
                    IsSpecDrafted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSpecApproved = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCodeDone = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCodeReviewApproved = table.Column<bool?>(type: "INTEGER", nullable: true),
                    IsAcPassed = table.Column<bool?>(type: "INTEGER", nullable: true),
                    IsAbandoned = table.Column<bool>(type: "INTEGER", nullable: false),
                    CodeReviewRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Specs_Epics_EpicId",
                        column: x => x.EpicId,
                        principalTable: "Epics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EpicAudits_EpicId",
                table: "EpicAudits",
                column: "EpicId");

            migrationBuilder.CreateIndex(
                name: "IX_Specs_EpicId",
                table: "Specs",
                column: "EpicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EpicAudits");
            migrationBuilder.DropTable(name: "Specs");
            migrationBuilder.DropTable(name: "Epics");
        }
    }
}
