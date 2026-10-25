using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProcessCancellationRecovery : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RequireOwnedIndex(migrationBuilder);
            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_process_runtime_states_stopped_root"
                ON process_runtime_states ("RunId") WHERE "RootRunId" = "RunId" AND "Status" IN ('CancelRequested', 'Cancelled')
                """, suppressTransaction: true);
            RequireOwnedIndex(migrationBuilder);
            migrationBuilder.CreateTable(
                name: "process_cancellation_recovery",
                columns: table => new
                {
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    BarrierCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseGeneration = table.Column<long>(type: "bigint", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProgressRevision = table.Column<long>(type: "bigint", nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    PositionJson = table.Column<string>(type: "text", nullable: false),
                    IntentJson = table.Column<string>(type: "text", nullable: true),
                    Pending = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_cancellation_recovery", x => x.RootRunId);
                    table.CheckConstraint("CK_process_cancellation_recovery_identity", "\"RootRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND ((\"Origin\" = 0 AND \"BarrierCommandId\" IS NOT NULL AND \"BarrierCommandId\" <> '00000000-0000-0000-0000-000000000000'::uuid)\n    OR (\"Origin\" = 1 AND \"BarrierCommandId\" IS NULL))");
                    table.CheckConstraint("CK_process_cancellation_recovery_lease", "\"LeaseGeneration\" >= 0 AND \"ProgressRevision\" >= 0 AND \"Cycle\" >= 0\nAND ((\"LeaseToken\" IS NULL AND \"LeaseExpiresAtUtc\" IS NULL)\n    OR (\"LeaseToken\" IS NOT NULL AND \"LeaseToken\" <> '00000000-0000-0000-0000-000000000000'::uuid\n        AND \"LeaseGeneration\" > 0 AND \"LeaseExpiresAtUtc\" IS NOT NULL AND \"DueAtUtc\" = \"LeaseExpiresAtUtc\" AND NOT \"Completed\"))");
                    table.CheckConstraint("CK_process_cancellation_recovery_payload", "octet_length(\"PositionJson\") BETWEEN 2 AND 1024\nAND (\"IntentJson\" IS NULL OR octet_length(\"IntentJson\") BETWEEN 2 AND 8192)\nAND \"Pending\" BETWEEN 0 AND 12\nAND (NOT \"Completed\" OR (\"IntentJson\" IS NULL AND \"Pending\" = 0))");
                    table.ForeignKey(
                        name: "FK_process_cancellation_recovery_process_runtime_states_RootRu~",
                        column: x => x.RootRunId,
                        principalTable: "process_runtime_states",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_cancellation_recovery_seed",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    LastRootRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_cancellation_recovery_seed", x => x.Id);
                    table.CheckConstraint("CK_process_cancellation_recovery_seed", "\"Id\" = 1 AND \"Revision\" >= 0\nAND (\"LastRootRunId\" IS NULL OR (\"LastRootRunId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Revision\" > 0))\nAND (NOT \"Completed\" OR \"Revision\" > 0)");
                });

            migrationBuilder.InsertData(
                table: "process_cancellation_recovery_seed",
                columns: new[] { "Id", "Completed", "LastRootRunId", "Revision" },
                values: new object[] { 1, false, null, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_process_cancellation_recovery_due",
                table: "process_cancellation_recovery",
                columns: new[] { "Origin", "DueAtUtc", "RootRunId" },
                filter: "NOT \"Completed\"");
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql(DowngradeGuard, suppressTransaction: true);
            RequireOwnedIndex(migrationBuilder);
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS \"IX_process_runtime_states_stopped_root\"", suppressTransaction: true);
            migrationBuilder.Sql("LOCK TABLE process_cancellation_recovery, process_cancellation_recovery_seed IN ACCESS EXCLUSIVE MODE");
            migrationBuilder.Sql(DowngradeGuard);
            migrationBuilder.DropTable(name: "process_cancellation_recovery");
            migrationBuilder.DropTable(name: "process_cancellation_recovery_seed");
        }

        private const string DowngradeGuard = """
            DO $recovery$ BEGIN
                IF EXISTS (SELECT 1 FROM process_cancellation_recovery)
                    OR EXISTS (SELECT 1 FROM process_cancellation_recovery_seed WHERE "Revision" <> 0 OR "Completed" OR "LastRootRunId" IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot downgrade while retained Process cancellation recovery records or seed progress exist.';
                END IF;
            END $recovery$;
            """;

        private static void RequireOwnedIndex(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $recovery$
                DECLARE owned regclass := to_regclass('"IX_process_runtime_states_stopped_root"');
                BEGIN
                    IF owned IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid JOIN pg_am a ON a.oid = c.relam
                        WHERE i.indexrelid = owned AND i.indrelid = 'process_runtime_states'::regclass
                            AND i.indisvalid AND i.indisready AND i.indislive AND NOT i.indisunique
                            AND i.indnkeyatts = 1 AND i.indnatts = 1 AND i.indoption[0] = 0 AND i.indexprs IS NULL AND a.amname = 'btree'
                            AND pg_get_indexdef(i.indexrelid, 1, true) = '"RunId"'
                            AND pg_get_expr(i.indpred, i.indrelid) = $predicate$(("RootRunId" = "RunId") AND (("Status")::text = ANY ((ARRAY['CancelRequested'::character varying, 'Cancelled'::character varying])::text[])))$predicate$
                    ) THEN
                        RAISE EXCEPTION 'Process cancellation recovery index is invalid or has a conflicting definition. Inspect IX_process_runtime_states_stopped_root and repair it explicitly before retrying the migration.';
                    END IF;
                END $recovery$;
                """, suppressTransaction: true);
        }
    }
}
