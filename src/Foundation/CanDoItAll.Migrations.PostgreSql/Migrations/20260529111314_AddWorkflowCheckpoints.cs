using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Backend = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    TrustBoundary = table.Column<int>(type: "integer", nullable: false),
                    ResumeAvailability = table.Column<int>(type: "integer", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    BackendCheckpointId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PayloadReference = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ResumeUnavailableReason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowCheckpoints_ExternalRequestId",
                table: "AgentFramework_WorkflowCheckpoints",
                column: "ExternalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowCheckpoints_RunId_CreatedAtUtc",
                table: "AgentFramework_WorkflowCheckpoints",
                columns: new[] { "RunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowCheckpoints_RunId_Kind",
                table: "AgentFramework_WorkflowCheckpoints",
                columns: new[] { "RunId", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowCheckpoints");
        }
    }
}
