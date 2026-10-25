using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProcessRefreshDelivery : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s';");
            migrationBuilder.CreateTable(
                name: "process_decision_refresh_delivery",
                columns: table => new {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtocolVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseGeneration = table.Column<long>(type: "bigint", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProgressRevision = table.Column<long>(type: "bigint", nullable: false),
                    WakeRevision = table.Column<long>(type: "bigint", nullable: false),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_decision_refresh_delivery", x => new { x.RunId, x.AdmissionCommandId });
                    table.CheckConstraint("CK_process_refresh_delivery_identity", "\"ProtocolVersion\" = 1 AND \"RunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"RootRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND \"AdmissionCommandId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_process_refresh_delivery_lease", "\"LeaseGeneration\" >= 0 AND \"ProgressRevision\" >= 0 AND \"WakeRevision\" >= 0\nAND ((\"LeaseToken\" IS NULL AND \"LeaseExpiresAtUtc\" IS NULL)\n    OR (\"LeaseToken\" IS NOT NULL AND \"LeaseToken\" <> '00000000-0000-0000-0000-000000000000'::uuid\n        AND \"LeaseGeneration\" > 0 AND \"LeaseExpiresAtUtc\" IS NOT NULL\n        AND \"DueAtUtc\" = \"LeaseExpiresAtUtc\" AND \"Disposition\" = 0))");
                    table.CheckConstraint("CK_process_refresh_delivery_outcome", "(\"Disposition\" = 0 AND \"Reason\" BETWEEN 0 AND 4)\nOR (\"Disposition\" = 1 AND \"Reason\" = 5)\nOR (\"Disposition\" = 2 AND \"Reason\" BETWEEN 6 AND 9)\nOR (\"Disposition\" = 3 AND \"Reason\" BETWEEN 10 AND 12)");
                    table.ForeignKey(
                        name: "FK_process_decision_refresh_delivery_process_runtime_idempoten~",
                        columns: x => new { x.RunId, x.AdmissionCommandId },
                        principalTable: "process_runtime_idempotency_keys",
                        principalColumns: new[] { "RunId", "CommandId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_decision_refresh_delivery_process_runtime_states_Ro~",
                        column: x => x.RootRunId,
                        principalTable: "process_runtime_states",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_process_decision_refresh_delivery_RootRunId",
                table: "process_decision_refresh_delivery",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "IX_process_refresh_delivery_due",
                table: "process_decision_refresh_delivery",
                columns: new[] { "DueAtUtc", "RunId", "AdmissionCommandId" },
                filter: "\"Disposition\" = 0");
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                LOCK TABLE process_decision_refresh_delivery IN ACCESS EXCLUSIVE MODE;
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_decision_refresh_delivery) THEN
                        RAISE EXCEPTION 'Cannot discard retained Process refresh delivery evidence.';
                    END IF;
                END
                $guard$;
                """);
            migrationBuilder.DropTable(
                name: "process_decision_refresh_delivery");
        }
    }
}
