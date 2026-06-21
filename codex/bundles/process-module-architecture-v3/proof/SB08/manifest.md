# SB08 Persistence, Event Store, Outbox, Artifact Ledger Stores, And Projection Storage Proof Manifest

## Status

Completed on 2026-06-15.

## Implementation Summary

SB08 implements durable EF Core persistence behind the SB07 runtime ports. Runtime remains persistence-implementation-free. The Persistence project now owns EF entities, mappings, a runtime unit of work, event replay store, outbox store, artifact ledger store, projection snapshots, projector offsets, and projection dead letters. Application no longer references Persistence.

## Source Assertions

| Assertion | Source |
| --- | --- |
| Runtime exposes persistence-free ports for state, unit-of-work, event store, outbox, and artifact ledger writes. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs:6` |
| `ProcessPersistenceDbContext` owns runtime state, event, outbox, ledger, idempotency, and projection row sets. | `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceDbContext.cs:5` |
| EF table, key, unique-index, and lookup-index mappings are centralized in Persistence. | `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceConfigurations.cs:10` |
| `EfProcessRuntimeUnitOfWork` writes state, events, outbox, ledger, and idempotency through one commit path. | `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs:8` |
| Unit-of-work validation rejects outbox or artifact ledger rows that reference events outside the same mutation before persistence writes. | `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs:237` |
| Event replay supports global sequence replay and root-run sequence replay. | `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeEventStore.cs:9` |
| Outbox rows have explicit pending, locked, delivered, and retry lifecycle operations. | `repo://src/CanDoItAll.Processes.Persistence/EfProcessOutboxStore.cs:7` |
| Projection storage owns snapshots, offsets, and dead letters as derived state. | `repo://src/CanDoItAll.Processes.Persistence/EfProcessProjectionStore.cs:6` |
| Projection contracts are typed and versioned with `RuntimeProjectionV1`. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionContracts.cs:67`, `repo://src/CanDoItAll.Processes.Contracts/ProcessContractVersions.cs:9` |
| Application no longer references Persistence in the process boundary graph. | `repo://src/CanDoItAll.Processes.Application/CanDoItAll.Processes.Application.csproj` |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Proof |
| --- | --- | --- | --- | --- |
| `ProcessRuntimeStateEntity` | `EfProcessRuntimeUnitOfWork` | Runtime state store load/commit callers | Inserted or replaced per run; child rows are replaced with the snapshot | `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs:141`, `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:17` |
| `ProcessRuntimeEventEntity` | Unit of work and event store append | Replay store, outbox/projector consumers | Append-only rows with global sequence and root-run sequence | `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeEventStore.cs:9`, `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:80` |
| `ProcessOutboxMessageEntity` | Runtime mutation commit and outbox writer | Projection/outbox delivery workers | Pending, claimed, retried, delivered; tied to runtime event id | `repo://src/CanDoItAll.Processes.Persistence/EfProcessOutboxStore.cs:22`, `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:105` |
| `ProcessArtifactLedgerEventEntity` | Runtime mutation commit and ledger store | Artifact ledger readers and future projections | Appended with causal runtime event id and artifact content hash | `repo://src/CanDoItAll.Processes.Persistence/EfProcessArtifactLedgerStore.cs:5`, `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:17` |
| `ProcessRuntimeIdempotencyEntity` | Runtime unit of work | Duplicate command handling | One committed result per run and command id | `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs:60`, `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:64` |
| `ProcessProjectionSnapshotEntity` | Projectors through `IProcessProjectionStore` | Future UI/API projections | Upserted derived snapshot, never authoritative runtime state | `repo://src/CanDoItAll.Processes.Persistence/EfProcessProjectionStore.cs:8`, `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:131` |
| `ProcessProjectorOffsetEntity` | Projectors through `IProcessProjectionStore` | Replay/projector resume logic | Monotonic offset save per projector and shard | `repo://src/CanDoItAll.Processes.Persistence/EfProcessProjectionStore.cs:62`, `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:131` |
| `ProcessProjectionDeadLetterEntity` | Projectors through `IProcessProjectionStore` | Operator/replay triage | Written when projection cannot process an event; queried by projector and shard | `repo://src/CanDoItAll.Processes.Persistence/EfProcessProjectionStore.cs:116`, `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:131` |

## Tests And Command Proof

| Proof | Result |
| --- | --- |
| `bundle://proof/SB08/build-unit-sb08.txt` | Unit test project build passed with 0 warnings and 0 errors. |
| `bundle://proof/SB08/test-unit-sb08.txt` | Focused SB03-SB08 tests passed: 59/59. |
| `bundle://proof/SB08/build-solution-sb08.txt` | Full solution build passed with 0 warnings and 0 errors. |
| `bundle://proof/SB08/scans/runtime-forbidden-persistence-scan.txt` | Runtime has no EF, Npgsql, DbContext, Persistence project, or persistence entity references. |
| `bundle://proof/SB08/scans/ui-application-persistence-entity-scan.txt` | UI/Application/Web source has no Persistence project or persistence entity references. |
| `bundle://proof/SB08/scans/old-observation-symbol-scan.txt` | Active Process source and tests do not reference old observation/runtime symbols. |
| `bundle://proof/SB08/scans/anti-stub-scan.txt` | No TODO, placeholder, fake, or `NotImplementedException` markers in SB08 source/tests. |
| `bundle://proof/SB08/performance-scan-summary.json` | No sync waits, per-call HTTP/JSON/regex allocation, casing allocation, or `ContainsKey`; EF LINQ matches are database-side indexed store queries and test-only metadata checks. |
| `bundle://proof/SB08/persistence-migration-summary.md` | EF/PostgreSQL context, tables, keys, and indexes summarized. |
| `bundle://proof/SB08/source-assertions.txt` | Source line assertions for ports, stores, atomicity checks, mappings, projection contracts, and tests. |
| `bundle://proof/SB08/codeanalytics-snapshot-summary.txt` | CodeAnalytics snapshot `snap-20260615203450-71eef9ce` reports 0 diagnostics, 0 cycles, 12 EF entities, and no blocking errors. |
| `bundle://proof/SB08/bundle-validator-prepared-sb08.txt` | Prepared-stage bundle validator passed after SB08 proof/status synchronization. |
| `bundle://proof/SB08/changed-file-hashes.txt` | Portable SHA-256 hash proof for changed SB08 files. |

## Test Coverage Anchors

| Behavior | Test |
| --- | --- |
| Commit writes runtime state, event, outbox, ledger, and idempotency together. | `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:17` |
| Broken event/outbox atomicity is rejected before rows are written. | `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:40` |
| Duplicate runtime command returns the existing result without a second event append. | `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:64` |
| Stale original runtime state is rejected without overwriting the current committed state. | `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:80` |
| Replay store reads global and root sequence order. | `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:117` |
| Outbox store claims, retries, and marks delivery explicitly. | `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:144` |
| Projection store upserts snapshots, saves monotonic offsets, and writes dead letters. | `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:170` |
| Required unique constraints are declared in the EF model. | `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs:219` |
| Process boundary test prevents Application from depending on Persistence. | `repo://tests/CanDoItAll.Tests.Unit/ProcessModuleBoundaryTests.cs:73` |

## Red-Team Evidence

Negative-path coverage rejects an outbox message whose event is not part of the same mutation, rejects duplicate events inside one mutation, preserves duplicate command idempotency, rejects stale original runtime state without overwriting current state, and verifies required unique constraints through EF metadata. The Application-to-Persistence project reference was removed and the boundary test was tightened so the scan and test enforce the same architecture rule.

## Browser Validation

Not required. SB08 changes persistence, contracts, and unit tests only.

## Downstream Handoff

SB09 and SB10 can consume the durable ports and projection stores. Composition and app migration wiring must happen in later integration subbundles; those subbundles should configure `ProcessPersistenceDbContext` with PostgreSQL, register the concrete stores behind runtime/projection interfaces, and generate rollout migrations when the context becomes part of the deployed application.
