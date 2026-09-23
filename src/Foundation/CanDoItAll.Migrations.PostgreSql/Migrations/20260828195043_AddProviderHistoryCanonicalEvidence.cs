using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProviderHistoryCanonicalEvidence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "HistoryVersion",
                table: "Workspace_SharedProviderInvocations",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "ProviderKindSnapshot",
                table: "Workspace_SharedProviderInvocations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderNameSnapshot",
                table: "Workspace_SharedProviderInvocations",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HistoryAttemptsJson",
                table: "LlmChats_InvocationRecords",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "HistoryEvidenceJson",
                table: "AgentFramework_WorkflowUsageObservations",
                type: "jsonb",
                nullable: false,
                defaultValue: "null");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HistoryVersion",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropColumn(
                name: "ProviderKindSnapshot",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropColumn(
                name: "ProviderNameSnapshot",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropColumn(
                name: "HistoryAttemptsJson",
                table: "LlmChats_InvocationRecords");

            migrationBuilder.DropColumn(
                name: "HistoryEvidenceJson",
                table: "AgentFramework_WorkflowUsageObservations");
        }
    }
}
