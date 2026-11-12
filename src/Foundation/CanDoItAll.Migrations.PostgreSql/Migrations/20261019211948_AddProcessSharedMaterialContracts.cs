using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProcessSharedMaterialContracts : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropCheckConstraint(
                name: "CK_process_decision_material_scopes_identity",
                table: "process_decision_material_scopes");

            migrationBuilder.AlterColumn<int>(
                name: "Executor",
                table: "process_decision_material_scopes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "MaterialProtocol",
                table: "process_decision_material_scopes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_decision_material_scopes_identity",
                table: "process_decision_material_scopes",
                sql: "\"ScopeId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"DatabaseProfileId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"WorkspaceNamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"RootRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"OwnerRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND (\"StepInstanceId\" IS NULL OR \"StepInstanceId\" <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND \"ContractId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"ContractVersion\" > 0\nAND ((\"MaterialProtocol\" = 1 AND \"Executor\" IS NOT NULL AND \"Executor\" BETWEEN 0 AND 1)\n    OR (\"MaterialProtocol\" = 2 AND \"Executor\" IS NULL))");
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE process_decision_material_scopes, process_runtime_step_assignments, process_prepared_launches IN ACCESS EXCLUSIVE MODE;
                DO $content$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_decision_material_scopes WHERE "MaterialProtocol" <> 1)
                        OR EXISTS (SELECT 1 FROM process_runtime_step_assignments
                            WHERE ("DecisionDataPolicyJson"::jsonb->>'materialProtocol')::integer >= 2)
                        OR EXISTS (SELECT 1 FROM process_prepared_launches
                            WHERE jsonb_path_exists("PayloadJson"::jsonb, '$.initialCommit.initialAssignments[*].decisionDataPolicy ? (@.materialProtocol >= 2)')) THEN
                        RAISE EXCEPTION 'Cannot downgrade shared Process content contracts or their original admissions.';
                    END IF;
                END $content$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_process_decision_material_scopes_identity",
                table: "process_decision_material_scopes");

            migrationBuilder.DropColumn(
                name: "MaterialProtocol",
                table: "process_decision_material_scopes");

            migrationBuilder.AlterColumn<int>(
                name: "Executor",
                table: "process_decision_material_scopes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_decision_material_scopes_identity",
                table: "process_decision_material_scopes",
                sql: "\"ScopeId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"DatabaseProfileId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"WorkspaceNamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"RootRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"OwnerRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND (\"StepInstanceId\" IS NULL OR \"StepInstanceId\" <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND \"ContractId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"ContractVersion\" > 0 AND \"Executor\" BETWEEN 0 AND 1");
        }
    }
}
