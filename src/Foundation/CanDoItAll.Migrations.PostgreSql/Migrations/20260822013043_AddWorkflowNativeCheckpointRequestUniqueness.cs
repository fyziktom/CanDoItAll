using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddWorkflowNativeCheckpointRequestUniqueness : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "AgentFramework_WorkflowBackendCheckpointPayloads"
                        WHERE "BackendRequestId" IS NOT NULL
                          AND "BackendRequestPortId" IS NOT NULL
                        GROUP BY "SessionId", "BackendRequestId", "BackendRequestPortId"
                        HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'Cannot enforce workflow native checkpoint request uniqueness while duplicate session/request/port links exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_AF_WfBackendCheckpoints_NativeRequest",
                table: "AgentFramework_WorkflowBackendCheckpointPayloads",
                columns: new[] { "SessionId", "BackendRequestId", "BackendRequestPortId" },
                unique: true,
                filter: "\"BackendRequestId\" IS NOT NULL AND \"BackendRequestPortId\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AF_WfBackendCheckpoints_NativeRequest",
                table: "AgentFramework_WorkflowBackendCheckpointPayloads");
        }
    }
}
