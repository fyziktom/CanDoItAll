# SB31 Proof Manifest

## Status

- Subbundle: `SB31`
- Status: `Completed`
- Owned requirements: `R18`, `R06`, `R17`
- Owned raw notes: historical main DB native memory data export/retirement, read-only compatibility lifetime, id mapping, old-data skip/rollback behavior, and main `AppDbContext` model retirement without destructive table drops.

## Main Repository Context

- Main repo alias: `repo://`
- Main repo local root for this execution: `C:\repositories\CanDoItAll`
- Scope note: SB31 handles historical main database `CognitiveMemory_*` data. It does not implement a native-service import pipeline because the native service must own a proven import contract before old data is copied.

## Semantic Invariant Contract

- Contract: `bundle://proof/SB31/semantic-invariants.md`

## Changed File Hashes

The changed file hash inventory is captured in `bundle://proof/SB31/transcripts/file-size-and-hash-audit.txt`.

| File | After SHA-256 |
| --- | --- |
| `repo://src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | `365b9546ba861c4ecc86ae93c8fcaed9ab782a47e701a2fad4f8de3ea16aeba0` |
| `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/LegacyCognitiveMemoryExportContracts.cs` | `8047a15fd3db7572d67f55962b6358d3791724b2a7443acfe10243ff0cc93c3d` |
| `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/LegacyCognitiveMemoryExportService.cs` | `3a397c0890bc4c1f83b9771346b2c1cf332a440466f794e2d44d7bbd4210f5a4` |
| `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/PostgreSqlLegacyCognitiveMemoryDataReader.cs` | `39947e0be90e8032d00783cbab815977f47d0a836c02e85b3efca6a46c813385` |
| `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260706015654_RetireLegacyCognitiveMemoryMainDbModel.cs` | `604f34952ec5f55c453f3d76952a77b942601d0951281b868220840ebc7d01d1` |
| `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260706015654_RetireLegacyCognitiveMemoryMainDbModel.Designer.cs` | `3dcaa445121aa2cd034509667c44f409116a949a34e124ec2d4542d7433b3f47` |
| `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs` | `42eea89849154d9a90357236e51342bb34a7d21c0bd08de16d6f22ecef59c377` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/LegacyCognitiveMemoryExportServiceTests.cs` | `585de0515036e154d44fd8cb770722a508081865f9468ff9ef62ccd960ee3778` |
| `repo://docs/cognitive-memory/README.md` | `a8cfbc06d1c227057c0eb3a3f7016ddadca38f910876425bb9baca6ad9ecd9b1` |
| `repo://docs/cognitive-memory/operations/legacy-main-db-retirement.md` | `50198ddbaa1608f4346fa8211c386b3f07026b3d418e1300474d0c5541fad933` |
| `bundle://subbundles/31-data-migration-export-retirement-and-compatibility/README.md` | `c75fd4715546accb3238d0daef51fe81316f749d2babc25e011971b7ec6973db` |
| `bundle://README.md` | `70e95547c207467d95dd66032b98f11714d312fa0db7ffe0cf53199a5a272e0b` |
| `bundle://reviews/01-execution-report.md` | `0f7b4fbb9d3b8d77d58051241dd621020a6477a760ab1a182f31f90da43acdef` |

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Focused legacy export and PostgreSQL reader tests | `bundle://proof/SB31/transcripts/passing-legacy-cognitive-memory-export-tests.txt` |
| EF pending model changes | `bundle://proof/SB31/transcripts/ef-pending-model-changes.txt` |
| No-op retirement migration audit | `bundle://proof/SB31/transcripts/migration-retirement-audit.txt` |
| Legacy export source boundary audit | `bundle://proof/SB31/transcripts/legacy-export-source-boundary-audit.txt` |
| Anti-stub and XML doc audit | `bundle://proof/SB31/transcripts/anti-stub-and-xml-doc-audit.txt` |
| Main solution build | `bundle://proof/SB31/transcripts/main-solution-build.txt` |
| File size and hash audit | `bundle://proof/SB31/transcripts/file-size-and-hash-audit.txt` |
| Bundle prepared-stage validation after SB31 | `bundle://evidence/36-prepared-stage-validation-after-sb31.txt` |
| Closure artifact path audit | `bundle://proof/SB31/transcripts/closure-artifact-path-audit.txt` |

## Passing Proof

- Focused tests pass: 10 tests cover disabled compatibility, empty export, populated export, id-map extraction for context/source/correlation ids, duplicate blocking, partial reader failure, unsafe export ids, and PostgreSQL filtering of only `CognitiveMemory_*` base tables.
- EF pending-model proof exits `0` and reports no pending model changes after `RetireLegacyCognitiveMemoryMainDbModel`.
- Migration audit verifies the retirement migration contains no destructive `migrationBuilder` operations and the current `AppDbContextModelSnapshot` has no `CognitiveMemory_` table mappings.
- Source boundary audit verifies production export files do not reference the native module, native service repository, `CognitiveMemoryDbContext`, Qdrant, or SemanticCompletion and contain no SQL write keywords.
- Main solution build passes with existing NU1900/NU1903 warnings only.
- Operator documentation records read-only retention, export artifacts, result semantics, removal trigger, rollback/skip behavior, and security handling for sensitive export data.

## Source Assertions

- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/LegacyCognitiveMemoryExportContracts.cs` defines typed export requests, result kinds, manifests, table manifests, row snapshots, reference map entries, and a factory boundary.
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/LegacyCognitiveMemoryExportService.cs` writes manifest JSON, per-table NDJSON, id-map NDJSON, SHA-256 hashes, duplicate protection, failure manifests, unsafe-id rejection, and failure logging without mutating the database.
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/PostgreSqlLegacyCognitiveMemoryDataReader.cs` reads PostgreSQL `CognitiveMemory_*` base tables from the current schema through parameterized metadata queries and quoted validated identifiers.
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260706015654_RetireLegacyCognitiveMemoryMainDbModel.cs` intentionally no-ops `Up` and `Down` so historical tables remain available as a read-only archive.
- `repo://docs/cognitive-memory/operations/legacy-main-db-retirement.md` defines the bounded compatibility lifetime and states that native import remains a future explicit native-service contract.
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LegacyCognitiveMemoryExportServiceTests.cs` exercises production export behavior and PostgreSQL table discovery.

## Browser Validation

- N/A. SB31 changes infrastructure export services, EF migration metadata, and operator documentation. It has no browser-visible UI surface.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Legacy export manifest | `LegacyCognitiveMemoryExportService` | Focused export tests deserialize and assert manifest semantics | Manifest records schema, result, retention, import policy, counts, and hashes | Failed export test verifies failed manifest behavior |
| Per-table NDJSON export | PostgreSQL reader plus export writer | Populated export test verifies table data output | One file per validated legacy table | Unsafe table and undeclared-column validation reject invalid snapshots |
| Reference id-map export | `ExtractReferences` over known reference columns | Populated export test verifies memory/source/correlation references | Id-map file supports manual native import planning | Columns not in known reference set stay in NDJSON instead of guessed mapping |
| Disabled and duplicate guards | Export request mode and manifest-exists check | Disabled and duplicate tests prove no DB read | Operators can skip or rerun safely with explicit overwrite | Duplicate export is blocked before reader invocation |
| PostgreSQL read-only reader | `PostgreSqlLegacyCognitiveMemoryDataReader` | PostgreSQL-backed unit test | Reads only current-schema `CognitiveMemory_*` base tables | Source audit rejects SQL write keywords |
| Main DbContext model retirement | No-op migration plus snapshot update | EF pending-model transcript and migration audit | Main model excludes legacy native registrations while old tables remain retained | Audit rejects destructive migration operations and snapshot legacy mappings |
| Operator runbook | `legacy-main-db-retirement.md` | Documentation hash and bundle validator | Compatibility ends only after validated native import or explicit deletion decision | Runbook rejects silent/lossy automatic import |

## Closure Decision

- SB31 closure gate: `Pass` after prepared validator and closure path audit hashes are recorded.
- Reopened subbundles: `None`.
- Downstream permission: SB32 may start because historical main DB data now has an explicit read-only export/retirement path and the main `AppDbContext` no longer carries native Cognitive Memory model registrations.
