using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddWorkflowRunOriginProjection : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginKind",
                table: "AgentFramework_WorkflowRuns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginProcessRunId",
                table: "AgentFramework_WorkflowRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginProjectId",
                table: "AgentFramework_WorkflowRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReportingActivityAtUtc",
                table: "AgentFramework_WorkflowRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $migration$
                BEGIN
                    PERFORM "OriginJson"::jsonb
                    FROM "AgentFramework_WorkflowRuns"
                    WHERE "OriginJson" LIKE '%"project-structure-node"%'
                       OR "OriginJson" LIKE '%"process-assignment"%';
                EXCEPTION
                    WHEN invalid_text_representation THEN
                        RAISE EXCEPTION 'Workflow origin projection backfill found malformed origin JSON.';
                END
                $migration$;

                WITH "OriginProjection" AS
                (
                    SELECT
                        "RunId",
                        CASE
                            WHEN "OriginJson" LIKE '%"project-structure-node"%' THEN 3
                            WHEN "OriginJson" LIKE '%"process-assignment"%' THEN 5
                        END AS "OriginKind",
                        substring(
                            "OriginJson"
                            FROM '"projectId"\s*:\s*"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})"')::uuid AS "OriginProjectId",
                        substring(
                            "OriginJson"
                            FROM '"processRunId"\s*:\s*"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})"')::uuid AS "OriginProcessRunId"
                    FROM "AgentFramework_WorkflowRuns"
                    WHERE "OriginJson" LIKE '%"project-structure-node"%'
                       OR "OriginJson" LIKE '%"process-assignment"%'
                )
                UPDATE "AgentFramework_WorkflowRuns" AS "Run"
                SET
                    "OriginKind" = "Projection"."OriginKind",
                    "OriginProjectId" = "Projection"."OriginProjectId",
                    "OriginProcessRunId" = "Projection"."OriginProcessRunId"
                FROM "OriginProjection" AS "Projection"
                WHERE "Run"."RunId" = "Projection"."RunId";

                DO $migration$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM "AgentFramework_WorkflowRuns"
                        WHERE "OriginJson" LIKE '%"project-structure-node"%'
                          AND ("OriginKind" <> 3 OR "OriginProjectId" IS NULL)
                    ) THEN
                        RAISE EXCEPTION 'Workflow origin projection backfill found malformed project-structure-node origin JSON.';
                    END IF;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM "AgentFramework_WorkflowRuns"
                        WHERE "OriginJson" LIKE '%"process-assignment"%'
                          AND ("OriginKind" <> 5 OR "OriginProcessRunId" IS NULL)
                    ) THEN
                        RAISE EXCEPTION 'Workflow origin projection backfill found malformed process-assignment origin JSON.';
                    END IF;
                END
                $migration$;

                UPDATE "AgentFramework_WorkflowRuns"
                SET "ReportingActivityAtUtc" =
                    COALESCE("TerminalAtUtc", "UpdatedAtUtc");
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ReportingActivityAtUtc",
                table: "AgentFramework_WorkflowRuns",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_ProjectReportingActivity",
                table: "AgentFramework_WorkflowRuns",
                columns: new[] { "OriginProjectId", "OriginKind", "ReportingActivityAtUtc", "RunId" },
                descending: new[] { false, false, true, true });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowRuns_ProjectReportingActivity",
                table: "AgentFramework_WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ReportingActivityAtUtc",
                table: "AgentFramework_WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "OriginKind",
                table: "AgentFramework_WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "OriginProcessRunId",
                table: "AgentFramework_WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "OriginProjectId",
                table: "AgentFramework_WorkflowRuns");
        }
    }
}
