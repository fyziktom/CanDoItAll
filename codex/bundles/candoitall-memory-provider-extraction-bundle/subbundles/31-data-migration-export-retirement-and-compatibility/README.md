# 31 Data Migration Export Retirement And Compatibility

## Status

- `Completed`

## Objective

- Implement data export/import or retirement path for existing main DB native memory tables and compatibility shims with documented lifetime.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R18
- R06
- R17

## Prerequisites

- SB30 completed

## Exact Source References

- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260528182412_InitialPostgreSqlBaseline.cs`
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260603113251_DisableCognitiveMemoryByDefault.cs`
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260706015654_RetireLegacyCognitiveMemoryMainDbModel.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/LegacyCognitiveMemoryExportContracts.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/LegacyCognitiveMemoryExportService.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/PostgreSqlLegacyCognitiveMemoryDataReader.cs`
- `repo://docs/cognitive-memory/operations/legacy-main-db-retirement.md`
- `bundle://architecture/06-native-service-extraction.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Completion Notes

- Implemented a read-only PostgreSQL export service for historical main DB `CognitiveMemory_*` tables with `manifest.json`, per-table NDJSON, SHA-256 hashes, and id-map NDJSON files.
- Chose explicit read-only archive over automatic native import. The native service must own a proven import contract before old rows are copied into the native DB.
- Added a no-op EF migration, `RetireLegacyCognitiveMemoryMainDbModel`, so the main `AppDbContext` snapshot no longer contains native Cognitive Memory model registrations while historical tables remain untouched.
- Documented retention, rollback, skip, duplicate export, failure, and removal-trigger behavior in `repo://docs/cognitive-memory/operations/legacy-main-db-retirement.md`.
- Added focused unit and PostgreSQL-backed tests for disabled, empty, populated, duplicate, partial failure, unsafe id, and table-filtering scenarios.

## Deliverables

- Implement or document data export/import, compatibility, retention, or retirement path for native memory records previously stored in the main database.
- Add migration/compatibility scripts or services that move/copy native data into the native service DB when enabled, or keep old data read-only for a bounded time.
- Remove native memory model registrations from the main AppDbContext after migration strategy is proven, while preserving migration history safety.
- Define compatibility shim lifetime and removal trigger.
- Add tests for export, id mapping, context pack/source refs, feedback correlation, old-data read-only behavior, and rollback/skip cases.

## Dependency Impact

- Final removal cannot proceed until existing data strategy is explicit and tested.

## Validation Depth

- `Migration safety`

## Implementation Steps

1. Inventory current migrations and native memory tables in the main database baseline.
2. Choose export/import, read-only compatibility, or retirement path and document decision tradeoffs.
3. Implement migration tooling or explicit manual procedure with checksums and id-mapping report.
4. Add tests for empty DB, existing native records, partial export failure, duplicate export, and compatibility disabled state.
5. Update docs and execution report with migration and rollback instructions.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- Existing native memory data has a clear migration or retirement path and does not force native tables into new main AppDbContext design.
- Feedback/context/source ids can be mapped or explicitly marked as not migratable with documented reason.
- Compatibility shims are bounded, observable, and not used to keep native memory coupled permanently.

## Proof Required

- Create `proof/SB31/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run migration/export tests for empty, populated, duplicate, partial failure, and disabled compatibility scenarios.
- Capture schema/model audit showing which native tables remain, move, or retire.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB31 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB31 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
