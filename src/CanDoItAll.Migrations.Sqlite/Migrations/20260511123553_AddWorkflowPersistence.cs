using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    NodeId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    Modality = table.Column<int>(type: "INTEGER", nullable: false),
                    ComponentJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowComponents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowDefinitions",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PreferredBackend = table.Column<int>(type: "INTEGER", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowDefinitions", x => x.VersionId);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    NodeId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowExternalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    NodeId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    RequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowExternalRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowRuns",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Backend = table.Column<int>(type: "INTEGER", nullable: false),
                    BackendRunId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowRuns", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowArtifacts_RunId_CreatedAtUtc",
                table: "AgentFramework_WorkflowArtifacts",
                columns: new[] { "RunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowComponents_Name",
                table: "AgentFramework_WorkflowComponents",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowComponents_ProviderProfileId",
                table: "AgentFramework_WorkflowComponents",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowDefinitions_WorkflowId",
                table: "AgentFramework_WorkflowDefinitions",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowDefinitions_WorkflowId_UpdatedAtUtc",
                table: "AgentFramework_WorkflowDefinitions",
                columns: new[] { "WorkflowId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowEvents_RunId_CreatedAtUtc",
                table: "AgentFramework_WorkflowEvents",
                columns: new[] { "RunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowExternalRequests_RunId_RespondedAtUtc",
                table: "AgentFramework_WorkflowExternalRequests",
                columns: new[] { "RunId", "RespondedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowRuns_UpdatedAtUtc",
                table: "AgentFramework_WorkflowRuns",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowRuns_WorkflowId",
                table: "AgentFramework_WorkflowRuns",
                column: "WorkflowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowArtifacts");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowComponents");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowEvents");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowExternalRequests");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowRuns");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowSettings");
        }
    }
}
