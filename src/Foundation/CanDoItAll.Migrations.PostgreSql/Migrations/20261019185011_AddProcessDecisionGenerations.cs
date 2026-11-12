using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProcessDecisionGenerations : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "DecisionReceiptJson",
                table: "process_runtime_idempotency_keys",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "process_decision_heads",
                columns: table => new {
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentGeneration = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_decision_heads", x => x.DecisionId);
                    table.CheckConstraint("CK_process_decision_heads_version", "\"CurrentGeneration\" BETWEEN 1 AND 32 AND \"Revision\" >= 2 * \"CurrentGeneration\" - 1");
                    table.ForeignKey(
                        name: "FK_process_decision_heads_process_decision_budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "process_decision_budgets",
                        principalColumn: "BudgetId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_heads_process_runtime_step_assignments_Run~",
                        columns: x => new { x.RunId, x.StepInstanceId },
                        principalTable: "process_runtime_step_assignments",
                        principalColumns: new[] { "RunId", "StepInstanceId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_decision_generations",
                columns: table => new {
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaptureAllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildCount = table.Column<int>(type: "integer", nullable: false),
                    UnclosedChildCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_decision_generations", x => new { x.DecisionId, x.Generation });
                    table.CheckConstraint("CK_process_decision_generations_state", "\"Generation\" BETWEEN 1 AND 32 AND \"Revision\" >= 2 * \"Generation\" - 1\nAND \"Phase\" BETWEEN 0 AND 5 AND \"ChildCount\" BETWEEN 0 AND 64\nAND \"UnclosedChildCount\" BETWEEN 0 AND \"ChildCount\" AND \"Revision\" >= 1 + 2 * \"ChildCount\" - \"UnclosedChildCount\"\nAND (\"Phase\" < 3 OR \"UnclosedChildCount\" = 0)\nAND (\"Phase\" <> 5 OR \"Generation\" < 32)");
                    table.ForeignKey(
                        name: "FK_process_decision_generations_process_decision_budget_alloca~",
                        columns: x => new { x.BudgetId, x.CaptureAllocationId },
                        principalTable: "process_decision_budget_allocations",
                        principalColumns: new[] { "BudgetId", "AllocationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_generations_process_decision_heads_Decisio~",
                        column: x => x.DecisionId,
                        principalTable: "process_decision_heads",
                        principalColumn: "DecisionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_decision_children",
                columns: table => new {
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Owner = table.Column<int>(type: "integer", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerRevision = table.Column<long>(type: "bigint", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    AcceptedReceiptId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_decision_children", x => new { x.DecisionId, x.Generation, x.ChildId });
                    table.CheckConstraint("CK_process_decision_children_closure", "\"Owner\" BETWEEN 0 AND 1\nAND ((\"OwnerRevision\" IS NULL AND \"Outcome\" IS NULL AND \"AcceptedReceiptId\" IS NULL)\nOR (\"OwnerRevision\" IS NOT NULL AND \"OwnerRevision\" > 0 AND \"Outcome\" IS NOT NULL\nAND ((\"Outcome\" = 0 AND \"AcceptedReceiptId\" IS NOT NULL) OR (\"Outcome\" = 1 AND \"AcceptedReceiptId\" IS NULL))))");
                    table.ForeignKey(
                        name: "FK_process_decision_children_process_decision_budget_allocatio~",
                        columns: x => new { x.BudgetId, x.AllocationId },
                        principalTable: "process_decision_budget_allocations",
                        principalColumns: new[] { "BudgetId", "AllocationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_children_process_decision_generations_Deci~",
                        columns: x => new { x.DecisionId, x.Generation },
                        principalTable: "process_decision_generations",
                        principalColumns: new[] { "DecisionId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_runtime_idempotency_keys_decision",
                table: "process_runtime_idempotency_keys",
                sql: "\"DecisionReceiptJson\" IS NULL OR octet_length(\"DecisionReceiptJson\") BETWEEN 1 AND 16384");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_generations_BudgetId_CaptureAllocationId",
                table: "process_decision_generations",
                columns: new[] { "BudgetId", "CaptureAllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_heads_BudgetId",
                table: "process_decision_heads",
                column: "BudgetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_heads_RootRunId",
                table: "process_decision_heads",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_heads_RunId_StepInstanceId",
                table: "process_decision_heads",
                columns: new[] { "RunId", "StepInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_children_BudgetId_AllocationId",
                table: "process_decision_children",
                columns: new[] { "BudgetId", "AllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_children_DecisionId_ChildId",
                table: "process_decision_children",
                columns: new[] { "DecisionId", "ChildId" },
                unique: true);
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE process_runtime_idempotency_keys, process_decision_heads, process_decision_generations, process_decision_children IN ACCESS EXCLUSIVE MODE;
                DO $decision$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_decision_heads)
                        OR EXISTS (SELECT 1 FROM process_runtime_idempotency_keys WHERE "DecisionReceiptJson" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade retained Process decision generations.';
                    END IF;
                END $decision$;
                """);
            migrationBuilder.DropTable(
                name: "process_decision_children");

            migrationBuilder.DropTable(
                name: "process_decision_generations");

            migrationBuilder.DropTable(
                name: "process_decision_heads");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_runtime_idempotency_keys_decision",
                table: "process_runtime_idempotency_keys");

            migrationBuilder.DropColumn(
                name: "DecisionReceiptJson",
                table: "process_runtime_idempotency_keys");
        }
    }
}
