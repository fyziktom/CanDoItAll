# Legacy Main DB Retirement

The native Cognitive Memory module has been removed from base host composition. Existing installations may still have historical `CognitiveMemory_*` tables in the main application database. Those tables are treated as a read-only archive until the data is imported into the native service through a proven native import path or explicitly deleted by an operator-approved migration.

## Export Path

Use `ILegacyCognitiveMemoryExportServiceFactory` from the infrastructure layer with a PostgreSQL connection to the old main database.

```csharp
await using var connection = new NpgsqlConnection(mainDatabaseConnectionString);
var factory = serviceProvider.GetRequiredService<ILegacyCognitiveMemoryExportServiceFactory>();
var exporter = factory.Create(connection);

var result = await exporter.ExportAsync(new LegacyCognitiveMemoryExportRequest(
    exportRootPath,
    "2026-07-legacy-main-db",
    CompatibilityEnabled: true));
```

The exporter reads only base tables whose names start with `CognitiveMemory_` in the current PostgreSQL schema. It writes one directory per export id containing:

- `manifest.json`: schema version, result kind, retention policy, native import policy, table counts, row counts, and SHA-256 hashes.
- `{TableName}.ndjson`: one JSON row per legacy table row with stable column ordering.
- `{TableName}.id-map.ndjson`: extracted identity and correlation references for import planning.

The id-map report includes known reference columns such as `Id`, `MemoryRecordId`, `SourceManifestId`, `SourceItemId`, `SourceSnapshotId`, `RecallContextPackId`, `ContextPackId`, `RecallTraceId`, `FeedbackId`, `FeedbackHandle`, `CorrelationId`, `CausationId`, `OperationId`, and `OutcomeCorrelationId`. Columns not listed there remain available in the table NDJSON and must be mapped deliberately by the native import process.

## Result Semantics

| Result | Behavior |
| --- | --- |
| `Disabled` | Compatibility export is disabled. The database is not read and no export directory is created. |
| `NoLegacyData` | No legacy rows were exported. A manifest is still written for auditability. |
| `Exported` | One or more legacy rows were exported with table hashes and id-map files. |
| `DuplicateBlocked` | A manifest already exists for the export id and overwrite was not requested. The database is not read. |
| `Failed` | Export failed after starting. A failed manifest is written when possible and the original main DB data remains untouched. |

Export ids must be single safe path segments. `.`/`..`, nested paths, and platform path separators are rejected before any database read.

## Retention And Retirement

The EF migration `RetireLegacyCognitiveMemoryMainDbModel` updates the main `AppDbContext` model snapshot so new code no longer carries native Cognitive Memory model registrations. Its `Up` and `Down` methods are intentionally no-op. Applying it must not drop, recreate, or mutate historical `CognitiveMemory_*` tables.

The compatibility lifetime is bounded by this rule:

1. Keep historical main DB `CognitiveMemory_*` tables read-only.
2. Export them before enabling a native-service import or deletion plan.
3. Import into the native service only through an explicit native import contract that preserves source, context-pack, feedback, and correlation identifiers.
4. Delete or archive the old tables only after import validation or an explicit operator decision that the data is no longer needed.

There is no automatic native import in the main host. Silent or lossy import would be worse than a documented archive because the native service owns the future schema and import validation.

## Security

Export artifacts can contain source excerpts, recall context packs, feedback text, and correlation metadata. Store them under restricted ACLs, encrypt them if they leave the host, and treat hashes as integrity checks rather than anonymization. Logs record export id and failure state, not raw row payloads.

## Validation

Focused tests cover disabled compatibility, empty export, populated export with context/source/correlation id map, duplicate export blocking, partial export failure, unsafe export ids, and PostgreSQL table filtering. EF pending-model proof must report no pending model changes after the no-op retirement migration.
