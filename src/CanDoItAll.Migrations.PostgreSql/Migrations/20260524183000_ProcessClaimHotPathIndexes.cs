using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524183000_ProcessClaimHotPathIndexes")]
    public partial class ProcessClaimHotPathIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Processes_Outbox_PendingClaimOrder"
                ON "Processes_Outbox" ((COALESCE("NextAttemptAtUtc", "CreatedAtUtc")), "CreatedAtUtc")
                INCLUDE ("Id", "CommandKey", "ProcessRunId", "LeaseExpiresAtUtc")
                WHERE "Status" = 0;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Automation_EnvelopeDeliveries_DueClaimOrder"
                ON "Automation_EnvelopeDeliveries" ("AvailableAtUtc", "CreatedAtUtc")
                INCLUDE ("Id", "EnvelopeId", "State", "LockedAtUtc")
                WHERE "State" IN (0, 1, 2);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Workspace_ConnectorCommands_PendingClaimOrder"
                ON "Workspace_ConnectorCommands" ((COALESCE("NextAttemptAtUtc", "CreatedAtUtc")), "CreatedAtUtc")
                INCLUDE ("Id", "ProjectId", "ConnectorPluginKey", "CommandKey", "LeaseExpiresAtUtc")
                WHERE "Status" = 0 AND "ApprovalState" <> 1;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Workspace_ConnectorCommands_PendingClaimOrder";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Automation_EnvelopeDeliveries_DueClaimOrder";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Processes_Outbox_PendingClaimOrder";""");
        }
    }
}
