# SB31 Semantic Invariants

## Raw Note Closure

- Existing main DB `CognitiveMemory_*` tables are not silently dropped.
- Historical data can be exported to checksummed artifacts with table rows and identity/correlation maps.
- The old data path is read-only and bounded; it does not keep the native module coupled to the main host.
- The main `AppDbContext` model is retired from native Cognitive Memory registrations through a no-op migration and updated snapshot.
- Automatic native import is intentionally not implemented because the native service must own a proven import contract.
- Disabled, duplicate, partial failure, and unsafe export-id cases fail predictably without hidden fallback behavior.

## Shipped Behavior

- Infrastructure exposes `ILegacyCognitiveMemoryExportServiceFactory` for PostgreSQL main database export.
- Export writes `manifest.json`, `{TableName}.ndjson`, and `{TableName}.id-map.ndjson` under a single safe export-id directory.
- Export result kinds are typed: `Disabled`, `NoLegacyData`, `Exported`, `DuplicateBlocked`, and `Failed`.
- PostgreSQL reading is limited to current-schema base tables with names starting `CognitiveMemory_`.
- The no-op `RetireLegacyCognitiveMemoryMainDbModel` migration updates EF model history while preserving historical tables.
- Operator documentation defines the compatibility lifetime and removal trigger.

## Invariants

### SB31-I01 Read-Only Legacy Export

- Source raw note: existing native memory rows need export/retirement without destructive main DB behavior.
- Expected behavior: export reads legacy PostgreSQL tables and writes local artifacts without `INSERT`, `UPDATE`, `DELETE`, `DROP`, `TRUNCATE`, or `ALTER`.
- Disallowed shallow implementation: using a migration or script that drops old tables, writes back into legacy tables, or treats old rows as expendable.
- Passing proof: `bundle://proof/SB31/transcripts/passing-legacy-cognitive-memory-export-tests.txt` and `bundle://proof/SB31/transcripts/legacy-export-source-boundary-audit.txt`.
- Changed source files and hashes: `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/PostgreSqlLegacyCognitiveMemoryDataReader.cs` `39947e0be90e8032d00783cbab815977f47d0a836c02e85b3efca6a46c813385`.
- Red-team negative case: adding a SQL write keyword to the production export files fails the source-boundary audit.

### SB31-I02 Reference Mapping Without Guessing

- Source raw note: context pack/source refs and feedback/correlation ids must be mappable or explicitly bounded.
- Expected behavior: known identity, source, context, feedback, operation, and correlation columns are emitted to id-map NDJSON; all other columns remain in table NDJSON for deliberate future mapping.
- Disallowed shallow implementation: dropping identifiers, inventing native ids, or claiming a lossless native import path before the native service defines one.
- Passing proof: populated export test in `bundle://proof/SB31/transcripts/passing-legacy-cognitive-memory-export-tests.txt`.
- Changed source files and hashes: `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/LegacyCognitiveMemoryExportService.cs` `3a397c0890bc4c1f83b9771346b2c1cf332a440466f794e2d44d7bbd4210f5a4`.
- Red-team negative case: removing `MemoryRecordId`, `SourceItemId`, or `CorrelationId` from reference extraction fails the populated export test.

### SB31-I03 Predictable Disabled Duplicate Failure And Unsafe-Id Behavior

- Source raw note: compatibility shims must be bounded and observable, not silent fallbacks.
- Expected behavior: disabled compatibility does not read the database or create a directory; duplicate export blocks before DB read unless overwrite is explicit; partial failures write a failed manifest when possible; unsafe path ids are rejected before DB read.
- Disallowed shallow implementation: silently exporting under a different id, falling back to a mock reader, swallowing partial failures, or accepting nested/path-traversal export ids.
- Passing proof: disabled, duplicate, failure, and unsafe-id tests in `bundle://proof/SB31/transcripts/passing-legacy-cognitive-memory-export-tests.txt`.
- Changed source files and hashes: `repo://tests/Unit/CanDoItAll.Tests.Unit/LegacyCognitiveMemoryExportServiceTests.cs` `585de0515036e154d44fd8cb770722a508081865f9468ff9ef62ccd960ee3778`.
- Red-team negative case: accepting `..` or nested export ids fails the unsafe-id theory test.

### SB31-I04 Main DbContext Model Retirement Without Table Deletion

- Source raw note: remove native memory model registrations from main `AppDbContext` after strategy is proven, preserving migration history safety.
- Expected behavior: current model snapshot has no `CognitiveMemory_` table mappings, EF reports no pending model changes, and the retirement migration has no destructive operations.
- Disallowed shallow implementation: leaving model drift for future agents, deleting historical table migrations, or scaffolding EF drops that would remove retained data.
- Passing proof: `bundle://proof/SB31/transcripts/ef-pending-model-changes.txt` and `bundle://proof/SB31/transcripts/migration-retirement-audit.txt`.
- Changed source files and hashes: `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260706015654_RetireLegacyCognitiveMemoryMainDbModel.cs` `604f34952ec5f55c453f3d76952a77b942601d0951281b868220840ebc7d01d1`.
- Red-team negative case: adding `migrationBuilder.DropTable` or leaving `CognitiveMemory_` in the snapshot fails the migration audit.

### SB31-I05 Native Boundary Preservation

- Source raw note: do not re-couple generic memory or the main host to native Cognitive Memory implementation types.
- Expected behavior: SB31 production export files reference only infrastructure persistence abstractions and PostgreSQL metadata, not native module, native service, Qdrant, or SemanticCompletion types.
- Disallowed shallow implementation: importing native service contracts into the main export path, using `CognitiveMemoryDbContext`, or adding Qdrant/native dependencies back into infrastructure.
- Passing proof: `bundle://proof/SB31/transcripts/legacy-export-source-boundary-audit.txt` and `bundle://proof/SB31/transcripts/main-solution-build.txt`.
- Changed source files and hashes: `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/LegacyCognitiveMemoryExportContracts.cs` `8047a15fd3db7572d67f55962b6358d3791724b2a7443acfe10243ff0cc93c3d`.
- Red-team negative case: adding `CanDoItAll.Modules.CognitiveMemory`, `CanDoItAll.CognitiveMemory`, `CognitiveMemoryDbContext`, Qdrant, or SemanticCompletion references fails the source-boundary audit.

## Shallow-Pass Trap Rejections

- A migration that updates the model by dropping old tables fails the no-destructive-operation audit.
- A README-only export story fails the focused export and PostgreSQL reader tests.
- A DTO-only exporter that never reads PostgreSQL fails the table-filtering test.
- A broad data dump without id-map artifacts fails the populated export assertions.
- A hidden native import/fallback path fails source-boundary and documentation assertions.
- Leaving pending EF model changes fails the EF pending-model transcript.

## Adversarial Negative Proof

- Unsafe export ids are rejected before any reader call.
- Duplicate exports are blocked before any reader call.
- Disabled compatibility returns a typed `Disabled` result without filesystem or DB side effects.
- Partial reader failures return `Failed` and write a failed manifest when possible.
- Migration audit rejects destructive EF operations and old model mappings.
- Source audit rejects native implementation, Qdrant, SemanticCompletion, and SQL write leakage.

## Semantic Positive Proof

- Focused tests execute production export code and deserialize real manifest/id-map artifacts.
- PostgreSQL-backed test creates a legacy table plus a non-legacy table and proves only the legacy table is read.
- EF tooling proves the current model and snapshot are aligned after the retirement migration.
- Main solution build proves the export service, migration, tests, and docs compile with the current repo.
- Operator documentation defines how old data is retained, exported, secured, and ultimately removed.

## Downstream Dependency Check

- SB32 can rebalance old native tests without preserving native `AppDbContext` model registrations.
- SB33 can run e2e proof against explicit provider profiles with historical data handled as a read-only archive.
- SB34 can perform cleanup and release-gate review with a documented old-data removal trigger.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Export manifest | `LegacyCognitiveMemoryExportManifest` | Manifest deserialization tests | Records result, retention, import policy, counts, and hashes | Failure manifest test |
| Table NDJSON | `WriteRowsAsync` | Populated export test | One row per legacy table row | Snapshot validation rejects undeclared columns |
| Id-map NDJSON | `WriteReferenceMapAsync` | Populated export test | Known ids/correlation refs available for future native import | No guessed mapping for unknown columns |
| PostgreSQL reader | `PostgreSqlLegacyCognitiveMemoryDataReader` | PostgreSQL table-filtering test | Current-schema legacy base tables only | Non-legacy table ignored |
| No-op retirement migration | `RetireLegacyCognitiveMemoryMainDbModel` | EF pending-model and migration audit | Snapshot retired; historical tables retained | Drop/create operations rejected |
| Operator runbook | `legacy-main-db-retirement.md` | Docs hash and validator | Compatibility ends after import validation or explicit deletion | Silent import rejected |
