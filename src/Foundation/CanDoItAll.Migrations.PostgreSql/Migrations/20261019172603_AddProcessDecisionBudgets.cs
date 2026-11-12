using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProcessDecisionBudgets : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "DecisionBudgetReceiptJson",
                table: "process_runtime_idempotency_keys",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "process_decision_budgets",
                columns: table => new {
                    BudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    MaximumWorkTicks = table.Column<long>(type: "bigint", nullable: false),
                    MaximumReplans = table.Column<int>(type: "integer", nullable: false),
                    MaximumBasisAgeTicks = table.Column<long>(type: "bigint", nullable: false),
                    ConsumedWorkTicks = table.Column<long>(type: "bigint", nullable: false),
                    ReservedWorkTicks = table.Column<long>(type: "bigint", nullable: false),
                    ConsumedReplans = table.Column<long>(type: "bigint", nullable: false),
                    ReservedReplans = table.Column<long>(type: "bigint", nullable: false),
                    AllocationCount = table.Column<int>(type: "integer", nullable: false),
                    OverrunDetected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_decision_budgets", x => x.BudgetId);
                    table.CheckConstraint("CK_process_decision_budgets_balance", "\"MaximumWorkTicks\" > 0 AND \"MaximumReplans\" >= 0 AND \"MaximumBasisAgeTicks\" > 0\nAND \"ConsumedWorkTicks\" >= 0 AND \"ReservedWorkTicks\" >= 0 AND \"ConsumedReplans\" >= 0 AND \"ReservedReplans\" >= 0\nAND \"AllocationCount\" BETWEEN 0 AND 256 AND \"Revision\" >= \"AllocationCount\" + 1\nAND (\"AllocationCount\" > 0 OR (\"ConsumedWorkTicks\" = 0 AND \"ReservedWorkTicks\" = 0 AND \"ConsumedReplans\" = 0 AND \"ReservedReplans\" = 0 AND NOT \"OverrunDetected\"))\nAND (\"OverrunDetected\" OR (\"ReservedWorkTicks\" <= \"MaximumWorkTicks\" AND \"ConsumedWorkTicks\" <= \"MaximumWorkTicks\" - \"ReservedWorkTicks\"\nAND \"ReservedReplans\" <= \"MaximumReplans\" AND \"ConsumedReplans\" <= \"MaximumReplans\" - \"ReservedReplans\"))");
                    table.ForeignKey(
                        name: "FK_process_decision_budgets_process_runtime_step_assignments_R~",
                        columns: x => new { x.RunId, x.StepInstanceId },
                        principalTable: "process_runtime_step_assignments",
                        principalColumns: new[] { "RunId", "StepInstanceId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_decision_budget_allocations",
                columns: table => new {
                    BudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedWorkTicks = table.Column<long>(type: "bigint", nullable: false),
                    GrantedReplans = table.Column<int>(type: "integer", nullable: false),
                    OwnerWorkRevision = table.Column<long>(type: "bigint", nullable: true),
                    ConsumedWorkTicks = table.Column<long>(type: "bigint", nullable: true),
                    ConsumedReplans = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_decision_budget_allocations", x => new { x.BudgetId, x.AllocationId });
                    table.CheckConstraint("CK_process_decision_budget_allocations_amounts", "\"GrantedWorkTicks\" >= 0 AND \"GrantedReplans\" >= 0 AND (\"GrantedWorkTicks\" > 0 OR \"GrantedReplans\" > 0)\nAND ((\"OwnerWorkRevision\" IS NULL AND \"ConsumedWorkTicks\" IS NULL AND \"ConsumedReplans\" IS NULL)\nOR (\"OwnerWorkRevision\" IS NOT NULL AND \"OwnerWorkRevision\" > 0 AND \"ConsumedWorkTicks\" IS NOT NULL AND \"ConsumedWorkTicks\" >= 0\nAND \"ConsumedReplans\" IS NOT NULL AND \"ConsumedReplans\" >= 0))");
                    table.ForeignKey(
                        name: "FK_process_decision_budget_allocations_process_decision_budget~",
                        column: x => x.BudgetId,
                        principalTable: "process_decision_budgets",
                        principalColumn: "BudgetId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_runtime_idempotency_keys_decision_budget",
                table: "process_runtime_idempotency_keys",
                sql: "\"DecisionBudgetReceiptJson\" IS NULL OR octet_length(\"DecisionBudgetReceiptJson\") BETWEEN 1 AND 8192");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_budgets_RootRunId",
                table: "process_decision_budgets",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_budgets_RunId_StepInstanceId",
                table: "process_decision_budgets",
                columns: new[] { "RunId", "StepInstanceId" },
                unique: true);
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE process_runtime_idempotency_keys, process_decision_budgets, process_decision_budget_allocations IN ACCESS EXCLUSIVE MODE;
                DO $budget$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_decision_budgets)
                        OR EXISTS (SELECT 1 FROM process_runtime_idempotency_keys WHERE "DecisionBudgetReceiptJson" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade retained Process decision accounting.';
                    END IF;
                END $budget$;
                """);
            migrationBuilder.DropTable(
                name: "process_decision_budget_allocations");

            migrationBuilder.DropTable(
                name: "process_decision_budgets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_runtime_idempotency_keys_decision_budget",
                table: "process_runtime_idempotency_keys");

            migrationBuilder.DropColumn(
                name: "DecisionBudgetReceiptJson",
                table: "process_runtime_idempotency_keys");
        }
    }
}
