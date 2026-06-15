# SB10 Monitoring Projectors, Live/History Snapshots, And Projection Contracts Proof Manifest

## Status

Completed on 2026-06-15.

## Implementation Summary

SB10 adds event-first projection contracts, replay workers, source-generated projection serialization, live snapshots, historical timeline records, run detail/runtime canvas/artifact map projections, freshness and lag metadata, projection dead-letter handling, projection-history persistence, and UI-ready application query services. Projection writes are derived from runtime events; UI-facing queries read projection stores only.

## Changed Source

Changed source and test hashes are recorded in `bundle://proof/SB10/changed-file-hashes.txt`. Line counts are recorded in `bundle://proof/SB10/line-counts.txt`.

## Source Assertions

| Assertion | Source |
| --- | --- |
| Projection DTOs expose live, history, detail, canvas, artifact, incident, freshness, and lag read models. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionReadModels.cs:11`, `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionReadModels.cs:43`, `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionReadModels.cs:63` |
| Projection contracts include snapshot, history, offset, and dead-letter store operations. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionContracts.cs:87`, `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionContracts.cs:131` |
| Projection JSON uses a cached source-generated context and typed ID converters. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionJsonCodec.cs:10`, `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionJsonCodec.cs:166` |
| Replay advances offsets only after successful projection and dead-letters failed events. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionWorker.cs:48`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:52` |
| Runtime event projector writes derived snapshots and timeline history without mutating runtime state. | `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:6`, `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:85` |
| UI-facing live/history/detail queries read projection snapshots/history only. | `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:5`, `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:25`, `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:67` |
| Projection history is persisted separately from runtime event/state stores. | `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceEntities.cs:205`, `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceConfigurations.cs:174`, `repo://src/CanDoItAll.Processes.Persistence/EfProcessProjectionStore.cs:87` |
| Live last-hour semantics, active-run inclusion, freshness, and restricted diagnostics are tested. | `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:87`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:104`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:124`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:144` |

Additional source assertions are captured in `bundle://proof/SB10/source-assertions.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Proof |
| --- | --- | --- | --- | --- |
| `ProcessLiveProcessSnapshot` | `ProcessRuntimeProjectionProjector` | `ProcessRuntimeProjectionQueryService.GetLiveProcessesAsync` and future SB13 UI | Upserted per run from runtime events; completed runs older than the live window are filtered while active runs remain visible. | `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:37`, `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:25`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:87` |
| `ProcessTimelineEventProjection` | `ProcessRuntimeProjectionProjector` | `ProcessRuntimeProjectionQueryService.GetRunHistoryAsync` and future timeline UI | Appended as ordered projection history by global sequence; rebuildable by replay. | `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:85`, `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:67`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:21` |
| `ProcessRunDetailProjection` | `ProcessRuntimeProjectionProjector` | `ProcessRuntimeProjectionQueryService.GetRunDetailAsync` and future run-detail UI | Upserted per run with current status, last event, incidents, messages, runtime canvas, artifact map, and freshness. | `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:76`, `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:92`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:21` |
| Runtime canvas and artifact map projections | `ProcessRuntimeProjectionProjector` | Run-detail query and future canvas/artifact UI | Upserted per run as derived display read models from event metadata and artifact projection references. | `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:79`, `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:82` |
| `ProcessProjectionFreshness` | `ProcessRuntimeProjectionProjector` and query freshness aggregation | Live/history/detail consumers | Carries projected-at time, source event time, source sequence, projector name, and lag. | `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:172`, `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:112`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:124` |
| Projection offsets | `ProcessProjectionReplayWorker` | Replay worker and operations/replay tooling | Offset advances only after successful projection; failed events are left unadvanced and dead-lettered. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionWorker.cs:48`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:52` |
| Projection dead letters | `ProcessProjectionReplayWorker` | Operations/replay tooling | Failed projection attempts are persisted with event identity, attempt count, error type, and message. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionWorker.cs:88`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:52` |
| `ProcessProjectionHistoryEntity` | `EfProcessProjectionStore.AppendHistoryAsync` | `EfProcessProjectionStore.ReadHistoryAsync` | Persists historical projection records independent of runtime state/event rows. | `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceEntities.cs:205`, `repo://src/CanDoItAll.Processes.Persistence/EfProcessProjectionStore.cs:87`, `bundle://proof/SB10/codeanalytics-snapshot-summary.txt` |

## Tests And Command Proof

| Proof | Result |
| --- | --- |
| `bundle://proof/SB10/failing-first-process-projection-tests.txt` | Failing-first proof captured before implementation; projection worker/query/service types were missing. |
| `bundle://proof/SB10/test-unit-sb10.txt` | Focused SB10 projection tests passed: 6/6. |
| `bundle://proof/SB10/test-unit-sb10-process-slice.txt` | Focused SB03-SB10 process tests passed: 73/73. |
| `bundle://proof/SB10/build-unit-sb10.txt` | Unit test project build passed with 0 warnings and 0 errors. |
| `bundle://proof/SB10/build-solution-sb10.txt` | Full solution build passed with 0 warnings and 0 errors. |
| `bundle://proof/SB10/scans/old-observation-service-scan.txt` | Active source/tests/tools/templates contain no old observation service/dashboard usage. |
| `bundle://proof/SB10/scans/projector-runtime-mutation-scan.txt` | Projector/query code contains no runtime state mutation or runtime EF access. |
| `bundle://proof/SB10/scans/raw-diagnostic-projection-scan.txt` | Normal projection/Application paths do not expose raw diagnostic detail. |
| `bundle://proof/SB10/scans/ui-application-process-runtime-ef-read-scan.txt` | UI/Web/Application contain no direct Process runtime/persistence EF entity reads. |
| `bundle://proof/SB10/scans/anti-stub-scan.txt` | No TODO, placeholder, stub, or `NotImplementedException` markers in SB10 source/tests. |
| `bundle://proof/SB10/performance-scan-summary.json` | No sync waits, Thread.Sleep, Task.Run hot path, unbounded queue, or per-call HttpClient. JSON options allocation is cached source-generation setup. |
| `bundle://proof/SB10/projection-review.md` | Projection review passed with noted broad EF logging false positives. |
| `bundle://proof/SB10/codeanalytics-snapshot-summary.txt` | CodeAnalytics snapshot `snap-20260615221805-8a975694` loaded 6 scoped projects and 74 documents, found 0 diagnostics and no blocking errors. |
| `bundle://proof/SB10/bundle-validator-prepared-sb10.txt` | Prepared-stage bundle validator passed after SB10 proof/status synchronization. |
| `bundle://proof/SB10/changed-file-hashes.txt` | Portable SHA-256 hash proof for changed SB10 source/test files. |

## Test Coverage Anchors

| Behavior | Test |
| --- | --- |
| Replay projects live snapshots, run detail, history, and offsets. | `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:21` |
| Failed projection dead-letters the event without advancing offset. | `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:52` |
| Live last-hour query excludes old completed runs. | `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:87` |
| Live last-hour query includes active runs outside the window. | `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:104` |
| Freshness exposes projector lag. | `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:124` |
| Restricted events project diagnostic references without raw payload detail. | `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:144` |

## Red-Team Evidence

The shallow-pass trap is a query service that reconstructs runtime truth from runtime tables or a projector that leaks diagnostic payloads into UI read models. SB10 rejects that through source boundaries, scans, and tests: UI-facing queries call `IProcessProjectionStore`, projector scans show no runtime mutation path, process-specific UI/Application scans show no direct runtime EF reads, and restricted diagnostics are reduced to stable diagnostic references.

## Browser Validation

Not required. SB10 changes projection contracts, stores, application query services, and tests only. Browser validation is deferred to SB13, which owns the first visible Blazor projection UI.

## Downstream Handoff

SB11 can emit additional runtime events/facets consumed by these projectors. SB13 can build live/history/detail/canvas/artifact UI from `ProcessRuntimeProjectionQueryService` and the projection DTOs without reading runtime internals.
