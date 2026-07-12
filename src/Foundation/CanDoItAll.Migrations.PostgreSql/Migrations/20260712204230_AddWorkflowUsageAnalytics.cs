using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddWorkflowUsageAnalytics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginJson",
                table: "AgentFramework_WorkflowRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TerminalAtUtc",
                table: "AgentFramework_WorkflowRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "AgentFramework_WorkflowRuns"
                SET "TerminalAtUtc" = "UpdatedAtUtc"
                WHERE "State" IN (4, 5, 6)
                  AND "TerminalAtUtc" IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowUsageObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExecutorId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProducerKind = table.Column<int>(type: "integer", nullable: false),
                    InvocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ProviderNameKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ProviderKind = table.Column<int>(type: "integer", nullable: true),
                    TransportKind = table.Column<int>(type: "integer", nullable: true),
                    Model = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ModelKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SourcePhase = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    UsageStatus = table.Column<int>(type: "integer", nullable: false),
                    PricingStatus = table.Column<int>(type: "integer", nullable: false),
                    PricingProvenance = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    CachedInputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    ReasoningTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    ToolCallCount = table.Column<int>(type: "integer", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    PricingProfileHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PricingVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProviderRequestId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProviderResponseId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OriginJson = table.Column<string>(type: "TEXT", nullable: false),
                    OriginKind = table.Column<int>(type: "integer", nullable: true),
                    OriginProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginProcessAssignmentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowUsageObservations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowUsageObservations_NodeId_ExecutorId",
                table: "AgentFramework_WorkflowUsageObservations",
                columns: new[] { "NodeId", "ExecutorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowUsageObservations_OriginProcessRunId~",
                table: "AgentFramework_WorkflowUsageObservations",
                columns: new[] { "OriginProcessRunId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowUsageObservations_ProviderNameKey_Mo~",
                table: "AgentFramework_WorkflowUsageObservations",
                columns: new[] { "ProviderNameKey", "ModelKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowUsageObservations_RunId_RecordedAtUtc",
                table: "AgentFramework_WorkflowUsageObservations",
                columns: new[] { "RunId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowUsageObservations_WorkflowId_Recorde~",
                table: "AgentFramework_WorkflowUsageObservations",
                columns: new[] { "WorkflowId", "RecordedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowUsageObservations");

            migrationBuilder.DropColumn(
                name: "OriginJson",
                table: "AgentFramework_WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "TerminalAtUtc",
                table: "AgentFramework_WorkflowRuns");
        }
    }
}
