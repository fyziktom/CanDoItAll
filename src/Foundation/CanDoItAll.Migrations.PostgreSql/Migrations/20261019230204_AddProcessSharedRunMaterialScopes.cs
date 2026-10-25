using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProcessSharedRunMaterialScopes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionRunScopeReceiptJson",
                table: "process_runtime_idempotency_keys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SharedRunScopeId",
                table: "process_decision_heads",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "process_decision_run_scopes",
                columns: table => new
                {
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    CurrentMaterialId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalMemberships = table.Column<int>(type: "integer", nullable: false),
                    CurrentMemberships = table.Column<int>(type: "integer", nullable: false),
                    CaptureAllocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CaptureMaterialId = table.Column<Guid>(type: "uuid", nullable: true),
                    CaptureKind = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_decision_run_scopes", x => x.ScopeId);
                    table.CheckConstraint("CK_process_decision_run_scopes_state", "\"Revision\" >= 1 + \"TotalMemberships\" AND \"Phase\" BETWEEN 0 AND 5\nAND \"TotalMemberships\" BETWEEN 0 AND 256\nAND \"CurrentMemberships\" BETWEEN 0 AND \"TotalMemberships\"\nAND (\"CurrentMaterialId\" IS NOT NULL OR (\"TotalMemberships\" = 0 AND \"Phase\" NOT IN (2, 4)))\nAND (\"Phase\" <> 0 OR \"CurrentMaterialId\" IS NULL)\nAND ((\"Phase\" = 1 AND \"CaptureAllocationId\" IS NOT NULL AND \"CaptureMaterialId\" IS NOT NULL\n    AND \"CaptureKind\" IS NOT NULL AND \"CaptureKind\" BETWEEN 0 AND 2\n    AND ((\"CurrentMaterialId\" IS NULL) = (\"CaptureKind\" = 0)))\n    OR (\"Phase\" <> 1 AND \"CaptureAllocationId\" IS NULL AND \"CaptureMaterialId\" IS NULL AND \"CaptureKind\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_process_run_scope_capture_allocation",
                        columns: x => new { x.BudgetId, x.CaptureAllocationId },
                        principalTable: "process_decision_budget_allocations",
                        principalColumns: new[] { "BudgetId", "AllocationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_run_scope_current_material",
                        column: x => x.CurrentMaterialId,
                        principalTable: "process_decision_materials",
                        principalColumn: "MaterialId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_run_scope_material_scope",
                        column: x => x.ScopeId,
                        principalTable: "process_decision_material_scopes",
                        principalColumn: "ScopeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_run_scope_root_budget",
                        column: x => x.BudgetId,
                        principalTable: "process_decision_budgets",
                        principalColumn: "BudgetId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_decision_run_scope_members",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepInstanceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_decision_run_scope_members", x => new { x.DecisionId, x.Generation });
                    table.ForeignKey(
                        name: "FK_process_run_scope_member_assignment",
                        columns: x => new { x.RunId, x.StepInstanceId },
                        principalTable: "process_runtime_step_assignments",
                        principalColumns: new[] { "RunId", "StepInstanceId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_run_scope_member_decision",
                        columns: x => new { x.DecisionId, x.Generation },
                        principalTable: "process_decision_generations",
                        principalColumns: new[] { "DecisionId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_run_scope_member_material",
                        column: x => x.MaterialId,
                        principalTable: "process_decision_materials",
                        principalColumn: "MaterialId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_run_scope_member_scope",
                        column: x => x.ScopeId,
                        principalTable: "process_decision_run_scopes",
                        principalColumn: "ScopeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_idempotency_scope_receipt",
                table: "process_runtime_idempotency_keys",
                sql: "\"DecisionRunScopeReceiptJson\" IS NULL OR octet_length(\"DecisionRunScopeReceiptJson\") BETWEEN 1 AND 8192");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_heads_SharedRunScopeId",
                table: "process_decision_heads",
                column: "SharedRunScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_run_scope_members_MaterialId",
                table: "process_decision_run_scope_members",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_run_scope_members_RunId_StepInstanceId",
                table: "process_decision_run_scope_members",
                columns: new[] { "RunId", "StepInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_run_scope_members_ScopeId_MaterialId",
                table: "process_decision_run_scope_members",
                columns: new[] { "ScopeId", "MaterialId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_run_scopes_BudgetId",
                table: "process_decision_run_scopes",
                column: "BudgetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_run_scopes_BudgetId_CaptureAllocationId",
                table: "process_decision_run_scopes",
                columns: new[] { "BudgetId", "CaptureAllocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_run_scopes_CurrentMaterialId",
                table: "process_decision_run_scopes",
                column: "CurrentMaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_process_decision_heads_process_decision_run_scopes_SharedRu~",
                table: "process_decision_heads",
                column: "SharedRunScopeId",
                principalTable: "process_decision_run_scopes",
                principalColumn: "ScopeId",
                onDelete: ReferentialAction.Restrict);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE process_decision_run_scopes, process_decision_run_scope_members, process_decision_budgets,
                    process_decision_heads, process_runtime_idempotency_keys, process_runtime_step_assignments,
                    process_prepared_launches IN ACCESS EXCLUSIVE MODE;
                DO $scope$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_decision_run_scopes)
                        OR EXISTS (SELECT 1 FROM process_decision_run_scope_members)
                        OR EXISTS (SELECT 1 FROM process_decision_heads WHERE "SharedRunScopeId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM process_decision_budgets WHERE ("RunAdmissionJson"::jsonb->>'schemaVersion')::integer >= 2)
                        OR EXISTS (SELECT 1 FROM process_runtime_idempotency_keys WHERE "DecisionRunScopeReceiptJson" IS NOT NULL
                            OR ("DecisionReceiptJson"::jsonb->>'version')::integer >= 3)
                        OR EXISTS (SELECT 1 FROM process_runtime_step_assignments WHERE ("DecisionDataPolicyJson"::jsonb->>'schemaVersion')::integer >= 5)
                        OR EXISTS (SELECT 1 FROM process_prepared_launches
                            WHERE ("PayloadJson"::jsonb #>> '{initialCommit,initialRunDecisionAdmission,schemaVersion}')::integer >= 2
                                OR jsonb_path_exists("PayloadJson"::jsonb, '$.initialCommit.initialAssignments[*].decisionDataPolicy ? (@.schemaVersion >= 5)')) THEN
                        RAISE EXCEPTION 'Cannot downgrade original Process shared scope admissions, material authority or receipts.';
                    END IF;
                END $scope$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_process_decision_heads_process_decision_run_scopes_SharedRu~",
                table: "process_decision_heads");

            migrationBuilder.DropTable(
                name: "process_decision_run_scope_members");

            migrationBuilder.DropTable(
                name: "process_decision_run_scopes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_idempotency_scope_receipt",
                table: "process_runtime_idempotency_keys");

            migrationBuilder.DropIndex(
                name: "IX_process_decision_heads_SharedRunScopeId",
                table: "process_decision_heads");

            migrationBuilder.DropColumn(
                name: "DecisionRunScopeReceiptJson",
                table: "process_runtime_idempotency_keys");

            migrationBuilder.DropColumn(
                name: "SharedRunScopeId",
                table: "process_decision_heads");
        }
    }
}
