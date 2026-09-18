using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddWorkflowStructureReceipts : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowStructureOutputs",
                columns: table => new {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrencePath = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    PlanJson = table.Column<string>(type: "TEXT", nullable: false),
                    ReceiptJson = table.Column<string>(type: "TEXT", nullable: false),
                    NextInspectionAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssetDispatchStarted = table.Column<bool>(type: "boolean", nullable: false),
                    StoragePlacementIntentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_AgentFramework_WorkflowStructureOutputs", x => new { x.RunId, x.OccurrencePath, x.Slot });
                });

            migrationBuilder.CreateTable(
                name: "Storage_PlacementIntents",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlanJson = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    DeletionRequested = table.Column<bool>(type: "boolean", nullable: false),
                    WriteAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalDispatchConfirmedStopped = table.Column<bool>(type: "boolean", nullable: false),
                    ReceiptJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Storage_PlacementIntents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_WorkflowAdmissions",
                columns: table => new {
                    IntentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    NativeNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    AdmissionJson = table.Column<string>(type: "TEXT", nullable: false),
                    Delivery = table.Column<int>(type: "integer", nullable: false),
                    StatusJson = table.Column<string>(type: "TEXT", nullable: false),
                    DeliveryFinished = table.Column<bool>(type: "boolean", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ObservedRunUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Workbench_WorkflowAdmissions", x => x.IntentId);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_WorkflowContributionReceipts",
                columns: table => new {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrencePath = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    NativeObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    StoragePlacementIntentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreparedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NodeJson = table.Column<string>(type: "TEXT", nullable: false),
                    ReceiptJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Workbench_WorkflowContributionReceipts", x => new { x.RunId, x.OccurrencePath, x.Slot });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowStructureOutputs_IsComplete_NextInsp~",
                table: "AgentFramework_WorkflowStructureOutputs",
                columns: new[] { "IsComplete", "NextInspectionAtUtc", "RunId", "OccurrencePath", "Slot" });

            migrationBuilder.CreateIndex(
                name: "IX_Storage_PlacementIntents_ProjectId_State_Id",
                table: "Storage_PlacementIntents",
                columns: new[] { "ProjectId", "State", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Storage_PlacementIntents_StorageId_State_Id",
                table: "Storage_PlacementIntents",
                columns: new[] { "StorageId", "State", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkflowAdmissions_DeliveryFinished_NextAttemptAt~",
                table: "Workbench_WorkflowAdmissions",
                columns: new[] { "DeliveryFinished", "NextAttemptAtUtc", "IntentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkflowAdmissions_ProjectId_NodeId_Sequence",
                table: "Workbench_WorkflowAdmissions",
                columns: new[] { "ProjectId", "NodeId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkflowAdmissions_RunId",
                table: "Workbench_WorkflowAdmissions",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkflowContributionReceipts_ProjectId_NativeObje~",
                table: "Workbench_WorkflowContributionReceipts",
                columns: new[] { "ProjectId", "NativeObjectId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE "AgentFramework_WorkflowStructureOutputs", "Storage_PlacementIntents",
                    "Workbench_WorkflowAdmissions", "Workbench_WorkflowContributionReceipts" IN ACCESS EXCLUSIVE MODE;
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AgentFramework_WorkflowStructureOutputs")
                        OR EXISTS (SELECT 1 FROM "Storage_PlacementIntents")
                        OR EXISTS (SELECT 1 FROM "Workbench_WorkflowAdmissions")
                        OR EXISTS (SELECT 1 FROM "Workbench_WorkflowContributionReceipts") THEN
                        RAISE EXCEPTION 'Cannot remove Workflow or Storage recovery evidence. Retain the current schema or dispose of the entire explicitly selected profile.'
                            USING ERRCODE = 'P0001';
                    END IF;
                END
                $guard$;
                """);

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowStructureOutputs");

            migrationBuilder.DropTable(
                name: "Storage_PlacementIntents");

            migrationBuilder.DropTable(
                name: "Workbench_WorkflowAdmissions");

            migrationBuilder.DropTable(
                name: "Workbench_WorkflowContributionReceipts");
        }
    }
}
