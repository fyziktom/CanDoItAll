using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProcessRefreshCaptureJournal : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s';");
            migrationBuilder.CreateTable(
                name: "process_decision_refresh_captures",
                columns: table => new {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionGeneration = table.Column<int>(type: "integer", nullable: false),
                    OriginalBudgetRevision = table.Column<long>(type: "bigint", nullable: false),
                    GrantedWorkTicks = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialGeneration = table.Column<int>(type: "integer", nullable: false),
                    InputFingerprint = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false),
                    ProtocolVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    InvocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    ConsumedWorkTicks = table.Column<long>(type: "bigint", nullable: true),
                    MaterialFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CapturedAtUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                    Disposition = table.Column<int>(type: "integer", nullable: true),
                    SettlementCommandId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_decision_refresh_captures", x => new { x.RunId, x.ReservationCommandId });
                    table.CheckConstraint("CK_process_refresh_capture_identity", "\"ProtocolVersion\" = 1 AND \"RunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"RootRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"ReservationCommandId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"AdmissionCommandId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"AdmissionCommandId\" <> \"ReservationCommandId\"\nAND \"MaterialId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"MaterialGeneration\" BETWEEN 2 AND 32\nAND \"OriginalBudgetRevision\" > 0 AND \"GrantedWorkTicks\" BETWEEN 1 AND 100000000\nAND \"InputFingerprint\" ~ '^sha256:[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_process_refresh_capture_outcome", "((\"Outcome\" IS NULL AND \"ConsumedWorkTicks\" IS NULL)\n    OR (\"Outcome\" IS NOT NULL AND \"Outcome\" BETWEEN 1 AND 7 AND \"ConsumedWorkTicks\" IS NOT NULL AND \"ConsumedWorkTicks\" >= 0))\nAND (\"Outcome\" IS NULL OR \"Outcome\" <> 7 OR \"ConsumedWorkTicks\" = 0)\nAND ((\"Outcome\" IS NOT NULL AND \"Outcome\" = 1 AND \"ConsumedWorkTicks\" <= \"GrantedWorkTicks\"\n        AND \"MaterialFingerprint\" IS NOT NULL AND \"MaterialFingerprint\" ~ '^[0-9A-F]{64}$'\n        AND \"CapturedAtUtcTicks\" IS NOT NULL AND \"CapturedAtUtcTicks\" BETWEEN 1 AND 3155378975999999999)\n    OR ((\"Outcome\" IS NULL OR \"Outcome\" <> 1) AND \"MaterialFingerprint\" IS NULL AND \"CapturedAtUtcTicks\" IS NULL))");
                    table.CheckConstraint("CK_process_refresh_capture_phase", "(\"InvocationId\" IS NULL OR \"InvocationId\" <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND ((\"Phase\" = 1 AND \"Revision\" = 1 AND \"InvocationId\" IS NULL AND \"Outcome\" IS NULL)\n    OR (\"Phase\" = 2 AND \"Revision\" = 2 AND \"InvocationId\" IS NOT NULL AND \"Outcome\" IS NULL)\n    OR (\"Phase\" IN (3, 4) AND \"Outcome\" IS NOT NULL\n        AND ((\"InvocationId\" IS NULL AND \"Outcome\" = 7 AND \"Revision\" = \"Phase\" - 1)\n            OR (\"InvocationId\" IS NOT NULL AND \"Outcome\" BETWEEN 1 AND 6 AND \"Revision\" = \"Phase\"))))\nAND ((\"Phase\" = 4 AND \"Disposition\" IS NOT NULL AND \"Disposition\" IN (1, 2) AND \"SettlementCommandId\" IS NOT NULL\n        AND \"SettlementCommandId\" <> '00000000-0000-0000-0000-000000000000'::uuid)\n    OR (\"Phase\" <> 4 AND \"Disposition\" IS NULL AND \"SettlementCommandId\" IS NULL))\nAND (\"Disposition\" IS NULL OR \"Disposition\" <> 1 OR \"Outcome\" = 1)");
                    table.ForeignKey(
                        name: "FK_process_decision_refresh_captures_process_decision_budget_a~",
                        columns: x => new { x.BudgetId, x.AllocationId },
                        principalTable: "process_decision_budget_allocations",
                        principalColumns: new[] { "BudgetId", "AllocationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_refresh_captures_process_decision_generati~",
                        columns: x => new { x.DecisionId, x.DecisionGeneration },
                        principalTable: "process_decision_generations",
                        principalColumns: new[] { "DecisionId", "Generation" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_refresh_captures_process_decision_material~",
                        column: x => x.ScopeId,
                        principalTable: "process_decision_material_scopes",
                        principalColumn: "ScopeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_refresh_captures_process_runtime_idempoten~",
                        columns: x => new { x.RunId, x.AdmissionCommandId },
                        principalTable: "process_runtime_idempotency_keys",
                        principalColumns: new[] { "RunId", "CommandId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_refresh_captures_process_runtime_idempote~1",
                        columns: x => new { x.RunId, x.ReservationCommandId },
                        principalTable: "process_runtime_idempotency_keys",
                        principalColumns: new[] { "RunId", "CommandId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_refresh_captures_process_runtime_idempote~2",
                        columns: x => new { x.RunId, x.SettlementCommandId },
                        principalTable: "process_runtime_idempotency_keys",
                        principalColumns: new[] { "RunId", "CommandId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_refresh_captures_process_runtime_states_Ro~",
                        column: x => x.RootRunId,
                        principalTable: "process_runtime_states",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_decision_refresh_capture_payloads",
                columns: table => new {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_decision_refresh_capture_payloads", x => new { x.RunId, x.ReservationCommandId });
                    table.CheckConstraint("CK_process_refresh_capture_payload", "octet_length(\"PayloadJson\") BETWEEN 2 AND 1048576");
                    table.ForeignKey(
                        name: "FK_process_decision_refresh_capture_payloads_process_decision_~",
                        columns: x => new { x.RunId, x.ReservationCommandId },
                        principalTable: "process_decision_refresh_captures",
                        principalColumns: new[] { "RunId", "ReservationCommandId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_refresh_captures_BudgetId_AllocationId",
                table: "process_decision_refresh_captures",
                columns: new[] { "BudgetId", "AllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_refresh_captures_DecisionId_DecisionGenera~",
                table: "process_decision_refresh_captures",
                columns: new[] { "DecisionId", "DecisionGeneration" });

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_refresh_captures_RootRunId",
                table: "process_decision_refresh_captures",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_refresh_captures_RunId_AdmissionCommandId",
                table: "process_decision_refresh_captures",
                columns: new[] { "RunId", "AdmissionCommandId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_refresh_captures_RunId_SettlementCommandId",
                table: "process_decision_refresh_captures",
                columns: new[] { "RunId", "SettlementCommandId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_refresh_captures_ScopeId",
                table: "process_decision_refresh_captures",
                column: "ScopeId");
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_decision_refresh_captures)
                        OR EXISTS (SELECT 1 FROM process_decision_refresh_capture_payloads) THEN
                        RAISE EXCEPTION 'Cannot downgrade retained Process refresh invocation evidence.';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.DropTable(
                name: "process_decision_refresh_capture_payloads");

            migrationBuilder.DropTable(
                name: "process_decision_refresh_captures");
        }
    }
}
