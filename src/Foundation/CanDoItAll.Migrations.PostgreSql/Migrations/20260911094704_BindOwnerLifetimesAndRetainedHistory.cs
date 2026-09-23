using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class BindOwnerLifetimesAndRetainedHistory : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropIndex(
                name: "IX_Projects_ProjectCreationReservations_ProjectId",
                table: "Projects_ProjectCreationReservations");

            migrationBuilder.AddColumn<Guid>(
                name: "DatabaseProfileId",
                table: "Workbench_WorkflowContributionReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportedHistory",
                table: "Workbench_WorkflowContributionReceipts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectLifetimeId",
                table: "Workbench_WorkflowContributionReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DatabaseProfileId",
                table: "Workbench_WorkflowAdmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportedHistory",
                table: "Workbench_WorkflowAdmissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectLifetimeId",
                table: "Workbench_WorkflowAdmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectLifetimeId",
                table: "Workbench_WorkAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectLifetimeId",
                table: "TestLab_TestPlans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectLifetimeId",
                table: "Resources_ProjectResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportedHistory",
                table: "Projects_ProjectRetirements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportedHistory",
                table: "Projects_ProjectCreationReservations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectLifetimeId",
                table: "CrmHr_StaffingRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectLifetimeId",
                table: "CrmHr_ProjectPartyAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DatabaseProfileId",
                table: "CrmHr_ProjectPartyAssignmentMoveReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceProjectLifetimeId",
                table: "CrmHr_ProjectPartyAssignmentMoveReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetProjectLifetimeId",
                table: "CrmHr_ProjectPartyAssignmentMoveReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DatabaseProfileId",
                table: "AgentFramework_WorkflowStructureOutputs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "AgentFramework_WorkflowStructureOutputs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectLifetimeId",
                table: "AgentFramework_WorkflowStructureOutputs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginProcessAssignmentId",
                table: "AgentFramework_WorkflowRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Workbench_ProcessAssetContributions",
                columns: table => new {
                    IntentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectLifetimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceExecutionRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    NativeObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageIntentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanJson = table.Column<string>(type: "TEXT", nullable: false),
                    PlanFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MaterializedRequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    MaterializedFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NodeJson = table.Column<string>(type: "TEXT", nullable: false),
                    ReceiptJson = table.Column<string>(type: "TEXT", nullable: false),
                    PreparedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ImportedHistory = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Workbench_ProcessAssetContributions", x => x.IntentId);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_WorkAssignmentHistory",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProjectLifetimeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ImportedHistory = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Workbench_WorkAssignmentHistory", x => x.Id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Workbench_WorkAssignments_ProjectLifetime",
                table: "Workbench_WorkAssignments",
                sql: "\"ProjectLifetimeId\" IS NULL OR (\n    \"ProjectLifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND \"ProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid)");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectCreationReservations_ProjectId",
                table: "Projects_ProjectCreationReservations",
                column: "ProjectId",
                unique: true,
                filter: "\"State\" = 1 AND \"ImportedHistory\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CrmHr_StaffingRequests_ProjectLifetime",
                table: "CrmHr_StaffingRequests",
                sql: "\"ProjectLifetimeId\" IS NULL OR (\n    \"ProjectLifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND \"ProjectId\" IS NOT NULL\n    AND \"ProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CrmHr_ProjectPartyAssignments_ProjectLifetime",
                table: "CrmHr_ProjectPartyAssignments",
                sql: "\"ProjectLifetimeId\" IS NULL OR (\n    \"ProjectLifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND \"ProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CrmHr_ProjectPartyAssignmentMoveReceipts_Lifetimes",
                table: "CrmHr_ProjectPartyAssignmentMoveReceipts",
                sql: "(\"DatabaseProfileId\" IS NULL AND \"SourceProjectLifetimeId\" IS NULL AND \"TargetProjectLifetimeId\" IS NULL)\nOR (\"DatabaseProfileId\" IS NOT NULL AND \"SourceProjectLifetimeId\" IS NOT NULL AND \"TargetProjectLifetimeId\" IS NOT NULL\n    AND \"DatabaseProfileId\" <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND \"SourceProjectLifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND \"TargetProjectLifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND \"SourceProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND \"TargetProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid)");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowStructureOutputs_RunId_ProjectId",
                table: "AgentFramework_WorkflowStructureOutputs",
                columns: new[] { "RunId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_ProcessAssignment",
                table: "AgentFramework_WorkflowRuns",
                columns: new[] { "OriginProcessRunId", "OriginProcessAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProcessAssetContributions_DatabaseProfileId_Proje~",
                table: "Workbench_ProcessAssetContributions",
                columns: new[] { "DatabaseProfileId", "ProjectId", "ProjectLifetimeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProcessAssetContributions_NativeObjectId",
                table: "Workbench_ProcessAssetContributions",
                column: "NativeObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProcessAssetContributions_SourceExecutionRunId",
                table: "Workbench_ProcessAssetContributions",
                column: "SourceExecutionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProcessAssetContributions_StorageIntentId",
                table: "Workbench_ProcessAssetContributions",
                column: "StorageIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkAssignmentHistory_AssignmentId",
                table: "Workbench_WorkAssignmentHistory",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkAssignmentHistory_ProjectId_NodeKey",
                table: "Workbench_WorkAssignmentHistory",
                columns: new[] { "ProjectId", "NodeKey" });

            migrationBuilder.Sql("""
                LOCK TABLE "AgentFramework_WorkflowRuns", "Projects_ProjectRetirements", "Projects_Projects",
                    "CrmHr_ProjectPartyAssignments", "CrmHr_StaffingRequests", "Resources_ProjectResources", "TestLab_TestPlans", "Workbench_WorkAssignments" IN ACCESS EXCLUSIVE MODE;

                CREATE FUNCTION pg_temp.owner_lifetimes_20260911094704_json(payload text)
                RETURNS json
                LANGUAGE plpgsql
                AS $inspection$
                DECLARE
                    whitespace constant text := U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000';
                    position integer := 1;
                    segment_start integer := 1;
                    payload_length integer;
                    quoted boolean := false;
                    current_character text;
                    inspection_copy text := '';
                BEGIN
                    IF payload IS NULL OR btrim(payload, whitespace) = '' THEN
                        RETURN NULL;
                    END IF;

                    IF strpos(payload, E'\\u0000') = 0 THEN
                        RETURN payload::json;
                    END IF;

                    PERFORM payload::json;
                    IF strpos(payload, E'\\u0000') = 0 THEN
                        RETURN payload::json;
                    END IF;
                    payload_length := char_length(payload);
                    WHILE position <= payload_length LOOP
                        current_character := substr(payload, position, 1);
                        IF quoted AND current_character = E'\\' THEN
                            IF substr(payload, position, 6) = E'\\u0000' THEN
                                inspection_copy := inspection_copy || substr(payload, segment_start, position - segment_start) || E'\\ufffd';
                                position := position + 6;
                                segment_start := position;
                            ELSE
                                position := position + 2;
                            END IF;
                        ELSE
                            IF current_character = '"' THEN
                                quoted := NOT quoted;
                            END IF;
                            position := position + 1;
                        END IF;
                    END LOOP;
                    RETURN (inspection_copy || substr(payload, segment_start))::json;
                END $inspection$;

                UPDATE "Resources_ProjectResources" AS owned
                SET "ProjectLifetimeId" = project."LifetimeId"
                FROM "Projects_Projects" AS project
                WHERE owned."ProjectId" = project."Id"
                    AND owned."ProjectId" <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND project."LifetimeId" <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND NOT EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS retired WHERE retired."ProjectId" = project."Id");

                UPDATE "TestLab_TestPlans" AS owned
                SET "ProjectLifetimeId" = project."LifetimeId"
                FROM "Projects_Projects" AS project
                WHERE owned."ProjectId" = project."Id"
                    AND owned."ProjectId" <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND project."LifetimeId" <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND NOT EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS retired WHERE retired."ProjectId" = project."Id");

                UPDATE "Workbench_WorkAssignments" AS owned
                SET "ProjectLifetimeId" = project."LifetimeId"
                FROM "Projects_Projects" AS project
                WHERE owned."ProjectId" = project."Id"
                    AND owned."ProjectId" <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND project."LifetimeId" <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND NOT EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS retired WHERE retired."ProjectId" = project."Id");

                UPDATE "CrmHr_ProjectPartyAssignments" AS owned
                SET "ProjectLifetimeId" = project."LifetimeId"
                FROM "Projects_Projects" AS project
                WHERE owned."ProjectId" = project."Id"
                    AND owned."ProjectId" <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND project."LifetimeId" <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND NOT EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS retired WHERE retired."ProjectId" = project."Id");

                UPDATE "CrmHr_StaffingRequests" AS owned
                SET "ProjectLifetimeId" = project."LifetimeId"
                FROM "Projects_Projects" AS project
                WHERE owned."ProjectId" = project."Id"
                    AND owned."ProjectId" <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND project."LifetimeId" <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND NOT EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" AS retired WHERE retired."ProjectId" = project."Id");

                DO $migration$
                DECLARE
                    saved record;
                    origin json;
                    process_run uuid;
                    assignment uuid;
                BEGIN
                    FOR saved IN SELECT "RunId", "OriginJson", "OriginKind", "OriginProcessRunId" FROM "AgentFramework_WorkflowRuns" LOOP
                        origin := pg_temp.owner_lifetimes_20260911094704_json(saved."OriginJson");
                        IF saved."OriginKind" = 5 OR origin ->> '$origin' = 'process-assignment' THEN
                            IF origin ->> '$origin' IS DISTINCT FROM 'process-assignment'
                                OR saved."OriginKind" IS NOT NULL AND saved."OriginKind" <> 5
                                OR json_typeof(origin -> 'processRunId') IS DISTINCT FROM 'string'
                                OR json_typeof(origin -> 'assignmentId') IS DISTINCT FROM 'string' THEN
                                RAISE EXCEPTION 'Cannot index inconsistent historical mapped Workflow origin for run %.', saved."RunId";
                            END IF;
                            process_run := (origin ->> 'processRunId')::uuid;
                            assignment := (origin ->> 'assignmentId')::uuid;
                            IF process_run = '00000000-0000-0000-0000-000000000000'::uuid
                                OR assignment = '00000000-0000-0000-0000-000000000000'::uuid
                                OR saved."OriginProcessRunId" IS NOT NULL AND saved."OriginProcessRunId" <> process_run THEN
                                RAISE EXCEPTION 'Cannot index conflicting historical mapped Workflow identity for run %.', saved."RunId";
                            END IF;
                            UPDATE "AgentFramework_WorkflowRuns"
                            SET "OriginProcessRunId" = process_run, "OriginProcessAssignmentId" = assignment
                            WHERE "RunId" = saved."RunId";
                        END IF;
                    END LOOP;
                END $migration$;

                DROP FUNCTION pg_temp.owner_lifetimes_20260911094704_json(text);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE "AgentFramework_WorkflowLaunchIdempotency",
                    "AgentFramework_WorkflowRuns",
                    "AgentFramework_WorkflowStructureOutputs",
                    "AgentFramework_WorkflowUsageObservations",
                    "CrmHr_ProjectPartyAssignmentMoveReceipts",
                    "CrmHr_ProjectPartyAssignments",
                    "CrmHr_StaffingRequests",
                    "Projects_ProjectCreationReservations",
                    "Projects_ProjectRetirements",
                    "Resources_ProjectResources",
                    "SchedulerPlanner_FireAdmissions",
                    "SchedulerPlanner_Plans",
                    "TestLab_TestPlans",
                    "Workbench_ProcessAssetContributions",
                    "Workbench_ProjectCrossModuleMutations",
                    "Workbench_WorkAssignmentHistory",
                    "Workbench_WorkAssignments",
                    "Workbench_WorkflowAdmissions",
                    "Workbench_WorkflowContributionReceipts" IN ACCESS EXCLUSIVE MODE;

                CREATE FUNCTION pg_temp.owner_lifetimes_20260911094704_json(payload text)
                RETURNS json
                LANGUAGE plpgsql
                AS $inspection$
                DECLARE
                    whitespace constant text := U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000';
                    position integer := 1;
                    segment_start integer := 1;
                    payload_length integer;
                    quoted boolean := false;
                    current_character text;
                    inspection_copy text := '';
                BEGIN
                    IF payload IS NULL OR btrim(payload, whitespace) = '' THEN
                        RETURN NULL;
                    END IF;

                    IF strpos(payload, E'\\u0000') = 0 THEN
                        RETURN payload::json;
                    END IF;

                    PERFORM payload::json;
                    IF strpos(payload, E'\\u0000') = 0 THEN
                        RETURN payload::json;
                    END IF;
                    payload_length := char_length(payload);
                    WHILE position <= payload_length LOOP
                        current_character := substr(payload, position, 1);
                        IF quoted AND current_character = E'\\' THEN
                            IF substr(payload, position, 6) = E'\\u0000' THEN
                                inspection_copy := inspection_copy || substr(payload, segment_start, position - segment_start) || E'\\ufffd';
                                position := position + 6;
                                segment_start := position;
                            ELSE
                                position := position + 2;
                            END IF;
                        ELSE
                            IF current_character = '"' THEN
                                quoted := NOT quoted;
                            END IF;
                            position := position + 1;
                        END IF;
                    END LOOP;
                    RETURN (inspection_copy || substr(payload, segment_start))::json;
                END $inspection$;

                DO $migration$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Workbench_ProcessAssetContributions")
                        OR EXISTS (SELECT 1 FROM "Workbench_WorkAssignmentHistory")
                        OR EXISTS (SELECT 1 FROM "Resources_ProjectResources" WHERE "ProjectLifetimeId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "TestLab_TestPlans" WHERE "ProjectLifetimeId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "Workbench_WorkAssignments" WHERE "ProjectLifetimeId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "CrmHr_ProjectPartyAssignments" WHERE "ProjectLifetimeId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "CrmHr_StaffingRequests" WHERE "ProjectLifetimeId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "CrmHr_ProjectPartyAssignmentMoveReceipts" WHERE "DatabaseProfileId" IS NOT NULL OR "SourceProjectLifetimeId" IS NOT NULL OR "TargetProjectLifetimeId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "Workbench_WorkflowContributionReceipts" WHERE "DatabaseProfileId" IS NOT NULL OR "ProjectLifetimeId" IS NOT NULL OR "ImportedHistory" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "Workbench_WorkflowAdmissions" WHERE "DatabaseProfileId" IS NOT NULL OR "ProjectLifetimeId" IS NOT NULL OR "ImportedHistory" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "AgentFramework_WorkflowStructureOutputs" WHERE "DatabaseProfileId" IS NOT NULL OR "ProjectId" IS NOT NULL OR "ProjectLifetimeId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" WHERE "ImportedHistory" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "Projects_ProjectCreationReservations" WHERE "ImportedHistory" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "AgentFramework_WorkflowRuns" WHERE
                        "OriginKind" IN (6, 7)
                        OR (pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #>> '{$origin}') IN ('process-tool-invocation-v1', 'process-dispatch-assignment-v1')
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #> '{structureAuthority,projectScope}')), 'null') <> 'null'
                        OR (pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #>> '{structureAuthority,channel}') IN ('agent-execution/project-scope-v1', 'authenticated-operator/project-scope-v1', 'local-operator/project-scope-v1', 'agent-execution/process-tool-scope-v1', 'authenticated-operator/process-tool-scope-v1', 'local-operator/process-tool-scope-v1'))
                        OR EXISTS (SELECT 1 FROM "AgentFramework_WorkflowUsageObservations" WHERE
                        "OriginKind" IN (6, 7)
                        OR (pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #>> '{$origin}') IN ('process-tool-invocation-v1', 'process-dispatch-assignment-v1')
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #> '{structureAuthority,projectScope}')), 'null') <> 'null'
                        OR (pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #>> '{structureAuthority,channel}') IN ('agent-execution/project-scope-v1', 'authenticated-operator/project-scope-v1', 'local-operator/project-scope-v1', 'agent-execution/process-tool-scope-v1', 'authenticated-operator/process-tool-scope-v1', 'local-operator/process-tool-scope-v1'))
                        OR EXISTS (SELECT 1 FROM "AgentFramework_WorkflowLaunchIdempotency" WHERE
                        "OriginKind" IN (6, 7)
                        OR (pg_temp.owner_lifetimes_20260911094704_json("CompletionJson") #>> '{run,origin,$origin}') IN ('process-tool-invocation-v1', 'process-dispatch-assignment-v1')
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("CompletionJson") #> '{run,origin,structureAuthority,projectScope}')), 'null') <> 'null'
                        OR (pg_temp.owner_lifetimes_20260911094704_json("CompletionJson") #>> '{run,origin,structureAuthority,channel}') IN ('agent-execution/project-scope-v1', 'authenticated-operator/project-scope-v1', 'local-operator/project-scope-v1', 'agent-execution/process-tool-scope-v1', 'authenticated-operator/process-tool-scope-v1', 'local-operator/process-tool-scope-v1')
                        OR (pg_temp.owner_lifetimes_20260911094704_json("CompletionJson") #>> '{resolvedRequest,origin,$origin}') IN ('process-tool-invocation-v1', 'process-dispatch-assignment-v1')
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("CompletionJson") #> '{resolvedRequest,origin,structureAuthority,projectScope}')), 'null') <> 'null'
                        OR (pg_temp.owner_lifetimes_20260911094704_json("CompletionJson") #>> '{resolvedRequest,origin,structureAuthority,channel}') IN ('agent-execution/project-scope-v1', 'authenticated-operator/project-scope-v1', 'local-operator/project-scope-v1', 'agent-execution/process-tool-scope-v1', 'authenticated-operator/process-tool-scope-v1', 'local-operator/process-tool-scope-v1'))
                        OR EXISTS (SELECT 1 FROM "Workbench_WorkflowAdmissions" WHERE
                        "Delivery" = 7
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("AdmissionJson") #> '{binding,authority,projectScope}')), 'null') <> 'null'
                        OR (pg_temp.owner_lifetimes_20260911094704_json("AdmissionJson") #>> '{binding,authority,channel}') IN ('agent-execution/project-scope-v1', 'authenticated-operator/project-scope-v1', 'local-operator/project-scope-v1', 'agent-execution/process-tool-scope-v1', 'authenticated-operator/process-tool-scope-v1', 'local-operator/process-tool-scope-v1')
                        OR (pg_temp.owner_lifetimes_20260911094704_json("AdmissionJson") #>> '{launchIntent,origin,$origin}') IN ('process-tool-invocation-v1', 'process-dispatch-assignment-v1')
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("AdmissionJson") #> '{launchIntent,origin,structureAuthority,projectScope}')), 'null') <> 'null'
                        OR (pg_temp.owner_lifetimes_20260911094704_json("AdmissionJson") #>> '{launchIntent,origin,structureAuthority,channel}') IN ('agent-execution/project-scope-v1', 'authenticated-operator/project-scope-v1', 'local-operator/project-scope-v1', 'agent-execution/process-tool-scope-v1', 'authenticated-operator/process-tool-scope-v1', 'local-operator/process-tool-scope-v1'))
                        OR EXISTS (SELECT 1 FROM "Workbench_WorkflowContributionReceipts" WHERE
                        COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("PlanJson") #> '{projectLifetime}')), 'null') <> 'null'
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("PlanJson") #> '{sourceAuthorityFingerprint}')), 'null') <> 'null'
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("ReceiptJson") #> '{projectLifetime}')), 'null') <> 'null'
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("ReceiptJson") #> '{sourceAuthorityFingerprint}')), 'null') <> 'null')
                        OR EXISTS (SELECT 1 FROM "AgentFramework_WorkflowStructureOutputs" WHERE
                        COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("PlanJson") #> '{projectLifetime}')), 'null') <> 'null'
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("PlanJson") #> '{sourceAuthorityFingerprint}')), 'null') <> 'null'
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("ReceiptJson") #> '{projectLifetime}')), 'null') <> 'null'
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("ReceiptJson") #> '{sourceAuthorityFingerprint}')), 'null') <> 'null')
                        OR EXISTS (SELECT 1 FROM "SchedulerPlanner_Plans" WHERE
                        COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("StructureAuthorityJson") #> '{projectScope}')), 'null') <> 'null'
                        OR (pg_temp.owner_lifetimes_20260911094704_json("StructureAuthorityJson") #>> '{channel}') IN ('agent-execution/project-scope-v1', 'authenticated-operator/project-scope-v1', 'local-operator/project-scope-v1', 'agent-execution/process-tool-scope-v1', 'authenticated-operator/process-tool-scope-v1', 'local-operator/process-tool-scope-v1'))
                        OR EXISTS (SELECT 1 FROM "SchedulerPlanner_FireAdmissions" WHERE
                        COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("SnapshotJson") #> '{authority,projectScope}')), 'null') <> 'null'
                        OR (pg_temp.owner_lifetimes_20260911094704_json("SnapshotJson") #>> '{authority,channel}') IN ('agent-execution/project-scope-v1', 'authenticated-operator/project-scope-v1', 'local-operator/project-scope-v1', 'agent-execution/process-tool-scope-v1', 'authenticated-operator/process-tool-scope-v1', 'local-operator/process-tool-scope-v1'))
                        OR EXISTS (SELECT 1 FROM "Workbench_ProjectCrossModuleMutations" WHERE
                        COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("PayloadJson") #> '{sourceReference}')), 'null') <> 'null'
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("PayloadJson") #> '{expectedTargetAdmission}')), 'null') <> 'null'
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("PayloadJson") #> '{SourceReference}')), 'null') <> 'null'
                        OR COALESCE(json_typeof((pg_temp.owner_lifetimes_20260911094704_json("PayloadJson") #> '{ExpectedTargetAdmission}')), 'null') <> 'null')
                        OR EXISTS (SELECT 1 FROM "AgentFramework_WorkflowRuns" WHERE
                        "OriginProcessAssignmentId" IS NOT NULL AND (
                            (pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #>> '{$origin}') IS DISTINCT FROM 'process-assignment'
                            OR "OriginKind" IS NOT NULL AND "OriginKind" <> 5
                            OR json_typeof((pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #> '{processRunId}')) IS DISTINCT FROM 'string'
                            OR json_typeof((pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #> '{assignmentId}')) IS DISTINCT FROM 'string'
                            OR ((pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #>> '{processRunId}'))::uuid IS DISTINCT FROM "OriginProcessRunId"
                            OR ((pg_temp.owner_lifetimes_20260911094704_json("OriginJson") #>> '{assignmentId}'))::uuid IS DISTINCT FROM "OriginProcessAssignmentId"
                            OR "OriginProcessRunId" IS NULL
                            OR "OriginProcessRunId" = '00000000-0000-0000-0000-000000000000'::uuid
                            OR "OriginProcessAssignmentId" = '00000000-0000-0000-0000-000000000000'::uuid)) THEN
                        RAISE EXCEPTION 'Cannot remove retained owner lifetime, imported history, Workflow source or cleanup evidence.';
                    END IF;
                END $migration$;

                DROP FUNCTION pg_temp.owner_lifetimes_20260911094704_json(text);
                """);

            migrationBuilder.DropTable(
                name: "Workbench_ProcessAssetContributions");

            migrationBuilder.DropTable(
                name: "Workbench_WorkAssignmentHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Workbench_WorkAssignments_ProjectLifetime",
                table: "Workbench_WorkAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ProjectCreationReservations_ProjectId",
                table: "Projects_ProjectCreationReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CrmHr_StaffingRequests_ProjectLifetime",
                table: "CrmHr_StaffingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CrmHr_ProjectPartyAssignments_ProjectLifetime",
                table: "CrmHr_ProjectPartyAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CrmHr_ProjectPartyAssignmentMoveReceipts_Lifetimes",
                table: "CrmHr_ProjectPartyAssignmentMoveReceipts");

            migrationBuilder.DropIndex(
                name: "IX_AgentFramework_WorkflowStructureOutputs_RunId_ProjectId",
                table: "AgentFramework_WorkflowStructureOutputs");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowRuns_ProcessAssignment",
                table: "AgentFramework_WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "DatabaseProfileId",
                table: "Workbench_WorkflowContributionReceipts");

            migrationBuilder.DropColumn(
                name: "ImportedHistory",
                table: "Workbench_WorkflowContributionReceipts");

            migrationBuilder.DropColumn(
                name: "ProjectLifetimeId",
                table: "Workbench_WorkflowContributionReceipts");

            migrationBuilder.DropColumn(
                name: "DatabaseProfileId",
                table: "Workbench_WorkflowAdmissions");

            migrationBuilder.DropColumn(
                name: "ImportedHistory",
                table: "Workbench_WorkflowAdmissions");

            migrationBuilder.DropColumn(
                name: "ProjectLifetimeId",
                table: "Workbench_WorkflowAdmissions");

            migrationBuilder.DropColumn(
                name: "ProjectLifetimeId",
                table: "Workbench_WorkAssignments");

            migrationBuilder.DropColumn(
                name: "ProjectLifetimeId",
                table: "TestLab_TestPlans");

            migrationBuilder.DropColumn(
                name: "ProjectLifetimeId",
                table: "Resources_ProjectResources");

            migrationBuilder.DropColumn(
                name: "ImportedHistory",
                table: "Projects_ProjectRetirements");

            migrationBuilder.DropColumn(
                name: "ImportedHistory",
                table: "Projects_ProjectCreationReservations");

            migrationBuilder.DropColumn(
                name: "ProjectLifetimeId",
                table: "CrmHr_StaffingRequests");

            migrationBuilder.DropColumn(
                name: "ProjectLifetimeId",
                table: "CrmHr_ProjectPartyAssignments");

            migrationBuilder.DropColumn(
                name: "DatabaseProfileId",
                table: "CrmHr_ProjectPartyAssignmentMoveReceipts");

            migrationBuilder.DropColumn(
                name: "SourceProjectLifetimeId",
                table: "CrmHr_ProjectPartyAssignmentMoveReceipts");

            migrationBuilder.DropColumn(
                name: "TargetProjectLifetimeId",
                table: "CrmHr_ProjectPartyAssignmentMoveReceipts");

            migrationBuilder.DropColumn(
                name: "DatabaseProfileId",
                table: "AgentFramework_WorkflowStructureOutputs");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "AgentFramework_WorkflowStructureOutputs");

            migrationBuilder.DropColumn(
                name: "ProjectLifetimeId",
                table: "AgentFramework_WorkflowStructureOutputs");

            migrationBuilder.DropColumn(
                name: "OriginProcessAssignmentId",
                table: "AgentFramework_WorkflowRuns");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectCreationReservations_ProjectId",
                table: "Projects_ProjectCreationReservations",
                column: "ProjectId",
                unique: true,
                filter: "\"State\" = 1");
        }
    }
}
