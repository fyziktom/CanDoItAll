using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProcessDecisionMaterials : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalMaterialId",
                table: "process_decision_generations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "process_decision_material_scopes",
                columns: table => new {
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceNamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Executor = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_decision_material_scopes", x => x.ScopeId);
                    table.CheckConstraint("CK_process_decision_material_scopes_identity", "\"ScopeId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"DatabaseProfileId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"WorkspaceNamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"RootRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"OwnerRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND (\"StepInstanceId\" IS NULL OR \"StepInstanceId\" <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND \"ContractId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"ContractVersion\" > 0 AND \"Executor\" BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_process_decision_material_scopes_process_runtime_states_Own~",
                        column: x => x.OwnerRunId,
                        principalTable: "process_runtime_states",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_material_scopes_process_runtime_step_assig~",
                        columns: x => new { x.OwnerRunId, x.StepInstanceId },
                        principalTable: "process_runtime_step_assignments",
                        principalColumns: new[] { "RunId", "StepInstanceId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_decision_materials",
                columns: table => new {
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    CapturedAtUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PayloadUtf8Bytes = table.Column<int>(type: "integer", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_decision_materials", x => x.MaterialId);
                    table.CheckConstraint("CK_process_decision_materials_content", "\"MaterialId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"Generation\" BETWEEN 1 AND 32\nAND \"CapturedAtUtcTicks\" BETWEEN 1 AND 3155378975999999999\nAND \"PayloadUtf8Bytes\" BETWEEN 2 AND 1048576\nAND octet_length(\"PayloadJson\") = \"PayloadUtf8Bytes\"\nAND \"Fingerprint\" ~ '^[0-9A-F]{64}$'");
                    table.ForeignKey(
                        name: "FK_process_decision_materials_process_decision_material_scopes~",
                        column: x => x.ScopeId,
                        principalTable: "process_decision_material_scopes",
                        principalColumn: "ScopeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_generations_CanonicalMaterialId",
                table: "process_decision_generations",
                column: "CanonicalMaterialId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_decision_generations_material",
                table: "process_decision_generations",
                sql: "\"CanonicalMaterialId\" IS NULL OR \"CanonicalMaterialId\" = \"MaterialId\"");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_material_scopes_OwnerRunId",
                table: "process_decision_material_scopes",
                column: "OwnerRunId",
                unique: true,
                filter: "\"StepInstanceId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_material_scopes_OwnerRunId_StepInstanceId",
                table: "process_decision_material_scopes",
                columns: new[] { "OwnerRunId", "StepInstanceId" },
                unique: true,
                filter: "\"StepInstanceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_material_scopes_RootRunId",
                table: "process_decision_material_scopes",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_materials_ScopeId_Generation",
                table: "process_decision_materials",
                columns: new[] { "ScopeId", "Generation" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_process_decision_generations_process_decision_materials_Can~",
                table: "process_decision_generations",
                column: "CanonicalMaterialId",
                principalTable: "process_decision_materials",
                principalColumn: "MaterialId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE process_runtime_idempotency_keys, process_decision_generations, process_decision_material_scopes, process_decision_materials,
                    process_runtime_step_assignments, process_prepared_launches IN ACCESS EXCLUSIVE MODE;
                DO $material$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_decision_material_scopes)
                        OR EXISTS (SELECT 1 FROM process_decision_materials)
                        OR EXISTS (SELECT 1 FROM process_decision_generations WHERE "CanonicalMaterialId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM process_runtime_idempotency_keys
                            WHERE "DecisionReceiptJson" IS NOT NULL AND "DecisionReceiptJson"::jsonb->>'version' <> '1')
                        OR EXISTS (SELECT 1 FROM process_runtime_step_assignments
                            WHERE ("DecisionDataPolicyJson"::jsonb->>'schemaVersion')::integer >= 3)
                        OR EXISTS (SELECT 1 FROM process_prepared_launches
                            WHERE jsonb_path_exists("PayloadJson"::jsonb, '$.initialCommit.initialAssignments[*].decisionDataPolicy ? (@.schemaVersion >= 3)')) THEN
                        RAISE EXCEPTION 'Cannot downgrade retained canonical Process material or versioned receipts.';
                    END IF;
                END $material$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_process_decision_generations_process_decision_materials_Can~",
                table: "process_decision_generations");

            migrationBuilder.DropTable(
                name: "process_decision_materials");

            migrationBuilder.DropTable(
                name: "process_decision_material_scopes");

            migrationBuilder.DropIndex(
                name: "IX_process_decision_generations_CanonicalMaterialId",
                table: "process_decision_generations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_decision_generations_material",
                table: "process_decision_generations");

            migrationBuilder.DropColumn(
                name: "CanonicalMaterialId",
                table: "process_decision_generations");
        }
    }
}
