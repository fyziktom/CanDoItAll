using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations;

public partial class IndexWorkflowProcessCancellation : Migration {
    private static readonly (string Name, string Table, string First, string Second, bool Pending)[] Selections = [
        ("IX_WorkflowRuns_ProcessRecovery", "AgentFramework_WorkflowRuns", "OriginProcessRunId", "RunId", false),
        ("IX_WorkflowRequests_ProcessRecovery", "AgentFramework_WorkflowExternalRequests", "RunId", "Id", true)
    ];

    protected override void Up(MigrationBuilder migrationBuilder) {
        foreach (var selection in Selections) {
            RequireOwnedIndex(migrationBuilder, selection);
        }
        foreach (var selection in Selections) {
            var predicate = selection.Pending ? " WHERE \"RespondedAtUtc\" IS NULL" : string.Empty;
            migrationBuilder.Sql($"CREATE INDEX CONCURRENTLY IF NOT EXISTS \"{selection.Name}\" ON \"{selection.Table}\" (\"{selection.First}\", \"{selection.Second}\"){predicate}",
                suppressTransaction: true);
            RequireOwnedIndex(migrationBuilder, selection);
        }
        migrationBuilder.Sql("""
            UPDATE process_cancellation_recovery_seed SET "LastRootRunId" = NULL, "Completed" = FALSE, "Revision" = "Revision" + 1
            WHERE "Id" = 1 AND ("Completed" OR "LastRootRunId" IS NOT NULL)
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        foreach (var selection in Selections) {
            RequireOwnedIndex(migrationBuilder, selection);
        }
        foreach (var selection in Selections) {
            migrationBuilder.Sql($"DROP INDEX CONCURRENTLY IF EXISTS \"{selection.Name}\"", suppressTransaction: true);
        }
    }

    private static void RequireOwnedIndex(MigrationBuilder migrationBuilder,
        (string Name, string Table, string First, string Second, bool Pending) selection) {
        var predicate = selection.Pending ? "pg_get_expr(i.indpred, i.indrelid) = '(\"RespondedAtUtc\" IS NULL)'" : "i.indpred IS NULL";
        migrationBuilder.Sql($"""
            DO $selection$
            DECLARE owned regclass := to_regclass('"{selection.Name}"');
            BEGIN
                IF owned IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid JOIN pg_am a ON a.oid = c.relam
                    WHERE i.indexrelid = owned AND i.indrelid = '"{selection.Table}"'::regclass
                        AND i.indisvalid AND i.indisready AND i.indislive AND NOT i.indisunique AND NOT i.indisprimary
                        AND NOT i.indisexclusion AND i.indnkeyatts = 2 AND i.indnatts = 2
                        AND 0 = ALL(i.indoption::smallint[]) AND 0 = ALL(i.indcollation::oid[])
                        AND i.indexprs IS NULL AND {predicate} AND a.amname = 'btree' AND c.relkind = 'i'
                        AND ARRAY(SELECT pg_get_indexdef(i.indexrelid, ordinal, true) FROM generate_series(1, 2) ordinal)
                            = ARRAY['"{selection.First}"', '"{selection.Second}"']::text[]
                        AND NOT EXISTS (SELECT 1 FROM unnest(i.indclass::oid[]) op(oid) JOIN pg_opclass oc ON oc.oid = op.oid
                            WHERE oc.opcnamespace <> 'pg_catalog'::regnamespace OR oc.opcname <> 'uuid_ops' OR oc.opcmethod <> c.relam)
                ) THEN
                    RAISE EXCEPTION 'Workflow Process recovery index {selection.Name} is invalid or has a conflicting definition. Inspect and repair it explicitly before retrying the migration.';
                END IF;
            END $selection$;
            """, suppressTransaction: true);
    }
}
