# SB08 Semantic Invariant Contract

## Status

Satisfied on 2026-06-15.

## Invariants

| Invariant | Evidence | Negative Proof |
| --- | --- | --- |
| SB08-INV-001: Runtime stays persistence-port-only and does not reference EF, Npgsql, DbContext, or persistence entities. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs:6`, CodeAnalytics snapshot `snap-20260615203450-71eef9ce` | `bundle://proof/SB08/scans/runtime-forbidden-persistence-scan.txt`; CodeAnalytics dependencies show Runtime depends on Core only. |
| SB08-INV-002: Runtime state, runtime events, outbox rows, artifact ledger rows, and idempotency rows commit through one explicit unit-of-work path. | `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs:8` | Atomic commit test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:17`; broken outbox event test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:40`. |
| SB08-INV-003: Outbox and artifact ledger rows cannot reference runtime events outside the same mutation. | `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs:237` | Broken atomicity test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:40`. |
| SB08-INV-004: Runtime command idempotency prevents duplicate event/outbox append for the same run and command. | `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs:76` | Duplicate command test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:64`; unique key proof in `bundle://proof/SB08/persistence-migration-summary.md`. |
| SB08-INV-005: Runtime events are append-only and replayable in global and root-run sequence order. | `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeEventStore.cs:9` | Replay test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:117`; unique root sequence index in `bundle://proof/SB08/scans/persistence-package-model-summary.txt`. |
| SB08-INV-006: Outbox delivery state is explicit and retryable; delivery is not silently swallowed. | `repo://src/CanDoItAll.Processes.Persistence/EfProcessOutboxStore.cs:22` | Outbox claim/retry/deliver test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:105`. |
| SB08-INV-007: Projection storage is derived state and cannot mutate runtime state. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionContracts.cs:106`, `repo://src/CanDoItAll.Processes.Persistence/EfProcessProjectionStore.cs:6` | Projection store test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:131`; UI/Application scan has no persistence row references. |
| SB08-INV-008: UI and Application do not consume persistence entities as runtime truth. | `repo://src/CanDoItAll.Processes.Application/CanDoItAll.Processes.Application.csproj`, CodeAnalytics snapshot `snap-20260615203450-71eef9ce` | `bundle://proof/SB08/scans/ui-application-persistence-entity-scan.txt`; boundary test at `repo://tests/CanDoItAll.Tests.Unit/ProcessModuleBoundaryTests.cs:73`. |
| SB08-INV-009: Old query-first observation symbols stay absent from active Process source. | Active Process source and tests | `bundle://proof/SB08/scans/old-observation-symbol-scan.txt`. |
| SB08-INV-010: SB08 persistence code avoids the listed .NET performance antipatterns. | `repo://codex/bundles/process-module-architecture-v3/architecture/19-dotnet-performance-guardrails.md` | `bundle://proof/SB08/performance-scan-summary.json`; EF LINQ matches are database-side queries with configured indexes, and test-only LINQ is metadata assertion code. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Test / Scan |
| --- | --- | --- | --- | --- |
| `ProcessPersistenceDbContext` | Persistence composition | Runtime/projection store implementations | Owns EF model and table mappings only in Persistence | CodeAnalytics persistence facts in `bundle://proof/SB08/codeanalytics-snapshot-summary.txt`; Runtime forbidden scan. |
| `ProcessRuntimeStateEntity` | Runtime unit of work | Runtime state store reads | Snapshot row with owned child collections; replaced only by unit-of-work commit | Atomic commit test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:17`. |
| `ProcessRuntimeEventEntity` | Runtime unit of work and event store | Replay/projector consumers | Append-only with global and root-run sequence constraints | Replay test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:80`; model constraint test. |
| `ProcessOutboxMessageEntity` | Runtime unit of work and outbox writer | Outbox delivery workers | Pending, locked, retried, delivered with lock ownership validation | Outbox lifecycle test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:105`. |
| `ProcessArtifactLedgerEventEntity` | Runtime unit of work and artifact ledger store | Artifact ledger readers and future projections | Appended with event id, slot id, artifact id, and content hash | Atomic commit test and model constraint test. |
| `ProcessRuntimeIdempotencyEntity` | Runtime unit of work | Duplicate command detection | One row per run and command id after a committed mutation | Duplicate command test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:64`. |
| `ProcessProjectionSnapshotEntity` | Projectors | Future UI/API projection readers | Upserted derived state by projector and projection key | Projection store test; UI/Application persistence entity scan. |
| `ProcessProjectorOffsetEntity` | Projectors | Replay resume logic | Monotonic offset per projector and shard | Projection store test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:131`. |
| `ProcessProjectionDeadLetterEntity` | Projectors | Retry and operator triage | Written when projection processing fails; read by projector and shard | Projection dead-letter test at `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:131`. |

## Validation Commands

```text
dotnet build tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --nologo
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --nologo --filter "FullyQualifiedName~ProcessPersistenceStoreTests|FullyQualifiedName~ProcessRuntimeEngineTests|FullyQualifiedName~ProcessInstancePlanCompilerTests|FullyQualifiedName~ProcessDriverAbstractionTests|FullyQualifiedName~ProcessTemplateGitFoundationTests|FullyQualifiedName~ProcessCoreKernelTests|FullyQualifiedName~ProcessModuleBoundaryTests"
dotnet build CanDoItAll.slnx --nologo
```

Results are captured in `bundle://proof/SB08/build-unit-sb08.txt`, `bundle://proof/SB08/test-unit-sb08.txt`, and `bundle://proof/SB08/build-solution-sb08.txt`.
