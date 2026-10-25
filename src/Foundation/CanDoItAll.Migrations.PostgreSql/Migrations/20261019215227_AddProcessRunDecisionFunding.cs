using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProcessRunDecisionFunding : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropIndex(
                name: "IX_process_decision_budgets_RunId_StepInstanceId",
                table: "process_decision_budgets");

            migrationBuilder.AlterColumn<Guid>(
                name: "StepInstanceId",
                table: "process_decision_budgets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AdmissionCommandId",
                table: "process_decision_budgets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentBudgetId",
                table: "process_decision_budgets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RunAdmissionJson",
                table: "process_decision_budgets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FundingAllocationId",
                table: "process_decision_budget_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FundingBudgetId",
                table: "process_decision_budget_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_budgets_ParentBudgetId",
                table: "process_decision_budgets",
                column: "ParentBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_budgets_RunId",
                table: "process_decision_budgets",
                column: "RunId",
                unique: true,
                filter: "\"StepInstanceId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_budgets_RunId_AdmissionCommandId",
                table: "process_decision_budgets",
                columns: new[] { "RunId", "AdmissionCommandId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_budgets_RunId_StepInstanceId",
                table: "process_decision_budgets",
                columns: new[] { "RunId", "StepInstanceId" },
                unique: true,
                filter: "\"StepInstanceId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_decision_budgets_owner",
                table: "process_decision_budgets",
                sql: "\"BudgetId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"RootRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"RunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND ((\"StepInstanceId\" IS NULL AND \"RunId\" = \"RootRunId\" AND \"ParentBudgetId\" IS NULL\n    AND \"AdmissionCommandId\" IS NOT NULL AND \"RunAdmissionJson\" IS NOT NULL\n    AND octet_length(\"RunAdmissionJson\") BETWEEN 1 AND 32768)\nOR (\"StepInstanceId\" IS NOT NULL AND \"StepInstanceId\" <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND \"AdmissionCommandId\" IS NULL AND \"RunAdmissionJson\" IS NULL\n    AND (\"ParentBudgetId\" IS NULL OR \"ParentBudgetId\" <> \"BudgetId\")))");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_budget_allocations_FundingBudgetId_Funding~",
                table: "process_decision_budget_allocations",
                columns: new[] { "FundingBudgetId", "FundingAllocationId" },
                unique: true,
                filter: "\"FundingBudgetId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_decision_budget_allocations_funding",
                table: "process_decision_budget_allocations",
                sql: "(\"FundingBudgetId\" IS NULL AND \"FundingAllocationId\" IS NULL)\nOR (\"FundingBudgetId\" IS NOT NULL AND \"FundingAllocationId\" IS NOT NULL AND \"FundingBudgetId\" <> \"BudgetId\")");

            migrationBuilder.AddForeignKey(
                name: "FK_process_decision_allocations_parent_funding",
                table: "process_decision_budget_allocations",
                columns: new[] { "FundingBudgetId", "FundingAllocationId" },
                principalTable: "process_decision_budget_allocations",
                principalColumns: new[] { "BudgetId", "AllocationId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_process_decision_budgets_admission",
                table: "process_decision_budgets",
                columns: new[] { "RunId", "AdmissionCommandId" },
                principalTable: "process_runtime_idempotency_keys",
                principalColumns: new[] { "RunId", "CommandId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_process_decision_budgets_parent",
                table: "process_decision_budgets",
                column: "ParentBudgetId",
                principalTable: "process_decision_budgets",
                principalColumn: "BudgetId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_process_decision_budgets_process_runtime_states_RunId",
                table: "process_decision_budgets",
                column: "RunId",
                principalTable: "process_runtime_states",
                principalColumn: "RunId",
                onDelete: ReferentialAction.Restrict);
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE process_decision_budgets, process_decision_budget_allocations, process_runtime_idempotency_keys,
                    process_runtime_step_assignments, process_prepared_launches IN ACCESS EXCLUSIVE MODE;
                DO $funding$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_decision_budgets WHERE "StepInstanceId" IS NULL OR "ParentBudgetId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM process_decision_budget_allocations WHERE "FundingBudgetId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM process_runtime_idempotency_keys
                            WHERE ("DecisionBudgetReceiptJson"::jsonb->>'version')::integer >= 2)
                        OR EXISTS (SELECT 1 FROM process_runtime_step_assignments
                            WHERE ("DecisionDataPolicyJson"::jsonb->>'schemaVersion')::integer >= 4)
                        OR EXISTS (SELECT 1 FROM process_prepared_launches
                            WHERE "PayloadJson"::jsonb #> '{initialCommit,initialRunDecisionAdmission}' IS NOT NULL
                                OR jsonb_path_exists("PayloadJson"::jsonb, '$.initialCommit.initialAssignments[*].decisionDataPolicy ? (@.schemaVersion >= 4)')) THEN
                        RAISE EXCEPTION 'Cannot downgrade original Process root funding, receipts or admissions.';
                    END IF;
                END $funding$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_process_decision_allocations_parent_funding",
                table: "process_decision_budget_allocations");

            migrationBuilder.DropForeignKey(
                name: "FK_process_decision_budgets_admission",
                table: "process_decision_budgets");

            migrationBuilder.DropForeignKey(
                name: "FK_process_decision_budgets_parent",
                table: "process_decision_budgets");

            migrationBuilder.DropForeignKey(
                name: "FK_process_decision_budgets_process_runtime_states_RunId",
                table: "process_decision_budgets");

            migrationBuilder.DropIndex(
                name: "IX_process_decision_budgets_ParentBudgetId",
                table: "process_decision_budgets");

            migrationBuilder.DropIndex(
                name: "IX_process_decision_budgets_RunId",
                table: "process_decision_budgets");

            migrationBuilder.DropIndex(
                name: "IX_process_decision_budgets_RunId_AdmissionCommandId",
                table: "process_decision_budgets");

            migrationBuilder.DropIndex(
                name: "IX_process_decision_budgets_RunId_StepInstanceId",
                table: "process_decision_budgets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_decision_budgets_owner",
                table: "process_decision_budgets");

            migrationBuilder.DropIndex(
                name: "IX_process_decision_budget_allocations_FundingBudgetId_Funding~",
                table: "process_decision_budget_allocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_decision_budget_allocations_funding",
                table: "process_decision_budget_allocations");

            migrationBuilder.DropColumn(
                name: "AdmissionCommandId",
                table: "process_decision_budgets");

            migrationBuilder.DropColumn(
                name: "ParentBudgetId",
                table: "process_decision_budgets");

            migrationBuilder.DropColumn(
                name: "RunAdmissionJson",
                table: "process_decision_budgets");

            migrationBuilder.DropColumn(
                name: "FundingAllocationId",
                table: "process_decision_budget_allocations");

            migrationBuilder.DropColumn(
                name: "FundingBudgetId",
                table: "process_decision_budget_allocations");

            migrationBuilder.AlterColumn<Guid>(
                name: "StepInstanceId",
                table: "process_decision_budgets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_budgets_RunId_StepInstanceId",
                table: "process_decision_budgets",
                columns: new[] { "RunId", "StepInstanceId" },
                unique: true);
        }
    }
}
