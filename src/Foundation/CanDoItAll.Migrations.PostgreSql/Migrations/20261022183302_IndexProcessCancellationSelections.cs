using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations;

public partial class IndexProcessCancellationSelections : Migration {
    private static readonly (string Table, string Key)[] Selections = [
        ("process_runtime_states", "RunId"),
        ("process_decision_heads", "DecisionId")
    ];

    protected override void Up(MigrationBuilder migrationBuilder) => ReplaceIndexes(migrationBuilder, composite: true);

    protected override void Down(MigrationBuilder migrationBuilder) => ReplaceIndexes(migrationBuilder, composite: false);

    private static void ReplaceIndexes(MigrationBuilder migrationBuilder, bool composite) {
        foreach (var (table, key) in Selections) {
            RequireOwnedIndex(migrationBuilder, table, key, composite: false);
            RequireOwnedIndex(migrationBuilder, table, key, composite: true);
        }
        foreach (var (table, key) in Selections) {
            var columns = composite ? $"\"RootRunId\", \"{key}\"" : "\"RootRunId\"";
            migrationBuilder.Sql($"CREATE INDEX CONCURRENTLY IF NOT EXISTS \"{IndexName(table, key, composite)}\" ON {table} ({columns})",
                suppressTransaction: true);
            RequireOwnedIndex(migrationBuilder, table, key, composite);
            RequireOwnedIndex(migrationBuilder, table, key, !composite);
            migrationBuilder.Sql($"DROP INDEX CONCURRENTLY IF EXISTS \"{IndexName(table, key, !composite)}\"", suppressTransaction: true);
        }
    }

    private static string IndexName(string table, string key, bool composite)
        => $"IX_{table}_RootRunId{(composite ? "_" + key : string.Empty)}";

    private static void RequireOwnedIndex(MigrationBuilder migrationBuilder, string table, string key, bool composite) {
        var name = IndexName(table, key, composite);
        var columns = composite ? $"'\"RootRunId\"', '\"{key}\"'" : "'\"RootRunId\"'";
        var count = composite ? 2 : 1;
        migrationBuilder.Sql($"""
            DO $selection$
            DECLARE owned regclass := to_regclass('"{name}"');
            BEGIN
                IF owned IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid JOIN pg_am a ON a.oid = c.relam
                    WHERE i.indexrelid = owned AND i.indrelid = '{table}'::regclass
                        AND i.indisvalid AND i.indisready AND i.indislive AND NOT i.indisunique AND NOT i.indisprimary
                        AND NOT i.indisexclusion AND i.indnkeyatts = {count} AND i.indnatts = {count}
                        AND 0 = ALL(i.indoption::smallint[]) AND 0 = ALL(i.indcollation::oid[])
                        AND i.indexprs IS NULL AND i.indpred IS NULL AND a.amname = 'btree' AND c.relkind = 'i'
                        AND ARRAY(SELECT pg_get_indexdef(i.indexrelid, ordinal, true) FROM generate_series(1, {count}) ordinal)
                            = ARRAY[{columns}]::text[]
                        AND NOT EXISTS (SELECT 1 FROM unnest(i.indclass::oid[]) op(oid) JOIN pg_opclass oc ON oc.oid = op.oid
                            WHERE oc.opcnamespace <> 'pg_catalog'::regnamespace OR oc.opcname <> 'uuid_ops' OR oc.opcmethod <> c.relam)
                ) THEN
                    RAISE EXCEPTION 'Process recovery selection index {name} is invalid or has a conflicting definition. Inspect and repair it explicitly before retrying the migration.';
                END IF;
            END $selection$;
            """, suppressTransaction: true);
    }
}
