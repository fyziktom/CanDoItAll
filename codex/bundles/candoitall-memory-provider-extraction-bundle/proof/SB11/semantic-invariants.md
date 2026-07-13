# SB11 Semantic Invariants

## Invariant SB11_SG001

- Invariant ID: `SB11_SG001`
- Source raw note: source gateway policy must control project source access by source kind and requested scope before module adapters execute.
- Expected behavior: source gateway rejects disallowed requested scopes with `DeniedSourceScope` and does not call the adapter.
- Disallowed shallow implementation: checking only the source kind, or relying on the Workbench adapter to reject policy mistakes after dispatch.
- Passing test: `SB11_SG001_Denied_requested_scope_fails_before_adapter_call` in `bundle://proof/SB11/transcripts/passing-source-gateway-tests.txt`.
- Changed source files: `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceGatewayContracts.cs` and `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceGateway.cs`.
- Production assertions: `bundle://proof/SB11/transcripts/source-audit-provider-driver-boundary.txt`.
- Red-team negative case: configuring a `Process` requested scope against a policy allowing only `Project` fails before adapter invocation.
- Downstream dependency check: SB12-SB14 can add more adapters using the same policy gate.

## Invariant SB11_WB001

- Invariant ID: `SB11_WB001`
- Source raw note: Workbench project snapshots must reuse the existing MAF source snapshot contract family.
- Expected behavior: Workbench registers an `IMemorySourceGatewayAdapter` that wraps `IProjectStructureSourceSnapshotProvider` and returns `CanDoItAll.AgentFramework.Core.MemorySourceSnapshot`.
- Disallowed shallow implementation: adding a duplicate generic memory snapshot DTO, exposing Workbench EF entities to providers, or replacing existing cursor/hash/redaction semantics.
- Passing test: Workbench project source integration tests in `bundle://proof/SB11/transcripts/passing-workbench-source-integration-tests.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureMemorySourceGatewayAdapter.cs` and `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureSourceSnapshotProvider.cs`.
- Production assertions: `bundle://proof/SB11/transcripts/source-audit-source-snapshot-contract-family.txt`.
- Red-team negative case: defining `MemorySourceSnapshot` in `src/Memory` or Workbench would fail the contract family audit.
- Downstream dependency check: provider-initiated source requests and user ingestion share a single snapshot model.

## Invariant SB11_WB002

- Invariant ID: `SB11_WB002`
- Source raw note: missing projects must have predictable unavailable-source semantics.
- Expected behavior: a missing project id returns a valid empty Workbench project snapshot with `EndOfSource`, zero items, and the requested project scope id.
- Disallowed shallow implementation: throwing from the source gateway for deleted projects, returning null, or fabricating provider success without a source snapshot.
- Passing test: `Workbench_gateway_adapter_returns_empty_snapshot_for_missing_project` in `bundle://proof/SB11/transcripts/passing-workbench-source-integration-tests.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureSourceSnapshotProvider.cs`.
- Production assertions: source snapshot integration transcript proves missing-project behavior uses production provider and database bootstrap.
- Red-team negative case: reverting to `GetStructureAsync` throws and breaks the integration test.
- Downstream dependency check: ingestion and provider source request paths can handle deleted project references deterministically.

## Invariant SB11_ING001

- Invariant ID: `SB11_ING001`
- Source raw note: manual project ingestion must create a generic source ingestion job tied to selected provider id and captured snapshot id.
- Expected behavior: `ProjectMemoryIngestionService` reads a source snapshot through `IMemorySourceGateway`, stores a `SnapshotCaptured` `MemorySourceIngestionJobRecord`, and persists the selected `MemoryProviderInstanceId` plus MAF snapshot id.
- Disallowed shallow implementation: queueing provider work before source capture, directly accessing provider drivers from Workbench, or omitting provider/snapshot identity from the ledger record.
- Passing test: `EnqueueProjectStructureIngestionAsync_captures_snapshot_and_persists_provider_job` in `bundle://proof/SB11/transcripts/passing-workbench-source-unit-tests.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectMemoryIngestionService.cs` and `repo://src/Memory/CanDoItAll.Memory.Application/MemorySourceIngestionContracts.cs`.
- Production assertions: Workbench registration guard and runtime composition bootstrap validate the service dependencies.
- Red-team negative case: denied gateway result throws and leaves the ledger empty.
- Downstream dependency check: SB20-SB22 UI can call a service that already enforces provider/source capture ordering.

## Invariant SB11_HOST001

- Invariant ID: `SB11_HOST001`
- Source raw note: Workbench ingestion must not add a dependency that breaks host DI or PostgreSQL bootstrap.
- Expected behavior: runtime composition registers the generic memory module before Workbench, design-time model discovery includes memory persistence, and PostgreSQL migration adds the generic `Memory_*` tables.
- Disallowed shallow implementation: registering Workbench ingestion without `IMemorySourceGateway`, suppressing EF pending-model-change warnings, or leaving memory tables outside migrations.
- Passing test: Workbench integration transcript in `bundle://proof/SB11/transcripts/passing-workbench-source-integration-tests.txt`.
- Changed source files: `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`, `repo://src/App/CanDoItAll.Composition/ModuleAssemblies.cs`, and `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260705163628_GenericMemoryProviderRuntime.cs`.
- Production assertions: `bundle://proof/SB11/transcripts/source-audit-memory-migration-scope.txt`.
- Red-team negative case: removing `AddGenericMemoryModule` or the migration causes integration bootstrap failure.
- Downstream dependency check: SB12-SB14 can add more source adapters against a registered generic source gateway.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| Project/workbench source adapters | Solved | `bundle://proof/SB11/manifest.md` and Workbench integration tests |
| Source Gateway scope gate for project snapshots | Solved | `SB11_SG001` and denied-scope integration proof |
| Manual project ingestion tied to provider and snapshot | Solved | `SB11_ING001` unit proof |
| Missing project unavailable-source semantics | Solved | `SB11_WB002` integration proof |
| No provider driver leakage of Workbench adapters | Solved | `bundle://proof/SB11/transcripts/source-audit-provider-driver-boundary.txt` |

## Shallow-Pass Trap

- A test-only adapter would not prove Workbench source behavior; SB11 integration tests use the real Workbench services and database bootstrap.
- A DTO-only source snapshot would satisfy compile checks but fork the contract family; the audit proves canonical MAF source snapshot reuse.
- A DI-only manual ingestion registration would miss ordering and identity; unit tests prove gateway capture before ledger enqueue with provider id and snapshot id.
- Suppressing EF pending model changes would hide runtime drift; SB11 adds a migration and records a migration scope audit.

## Downstream Dependency Check

- SB12-SB14 can add additional source adapters using the same `IMemorySourceGatewayAdapter` and policy behavior.
- SB15-SB18 can rely on source ingestion jobs carrying provider id, source request, and captured snapshot id.
- SB20-SB22 can surface manual project ingestion through UI without inventing a separate source capture path.
- SB30 remains protected because Workbench adapters live in Workbench and generic provider drivers contain no Workbench/project references.
