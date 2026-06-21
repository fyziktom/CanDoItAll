# SB10 Semantic Invariant Contract

## Status

Satisfied on 2026-06-15.

## Invariants

| Invariant | Evidence | Negative Proof |
| --- | --- | --- |
| SB10-INV-001: Projection state is derived and rebuildable from runtime events. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionWorker.cs:48`, `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:6` | Failing-first proof and replay tests at `bundle://proof/SB10/failing-first-process-projection-tests.txt` and `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:21`. |
| SB10-INV-002: Projectors do not mutate runtime state. | `repo://src/CanDoItAll.Processes.Projections/CanDoItAll.Processes.Projections.csproj`; CodeAnalytics dependency proof in `bundle://proof/SB10/codeanalytics-snapshot-summary.txt` | `bundle://proof/SB10/scans/projector-runtime-mutation-scan.txt`. |
| SB10-INV-003: UI-facing queries read projection stores only. | `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:5` | `bundle://proof/SB10/scans/ui-application-process-runtime-ef-read-scan.txt`; no direct Process runtime/persistence EF reads in UI/Web/Application. |
| SB10-INV-004: Live last-hour semantics are stable and explicit. | `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:18`, `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:38` | Tests at `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:87` and `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:104`. |
| SB10-INV-005: Projection freshness and lag are visible. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionReadModels.cs:11`, `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:172` | Freshness test at `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:124`. |
| SB10-INV-006: Raw diagnostics are restricted links, not normal projection text. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionReadModels.cs:41`, `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionReadModels.cs:72` | Restricted diagnostic test at `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:144`; `bundle://proof/SB10/scans/raw-diagnostic-projection-scan.txt`. |
| SB10-INV-007: Replay failure is explicit and does not silently advance offsets. | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionWorker.cs:88` | Dead-letter/offset test at `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:52`. |
| SB10-INV-008: Projection history persists separately from runtime state. | `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceEntities.cs:205`, `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceDbContext.cs:27` | CodeAnalytics persistence facts in `bundle://proof/SB10/codeanalytics-snapshot-summary.txt`; process-specific UI/Application runtime EF scan. |
| SB10-INV-009: Old observation-service runtime truth is not reintroduced. | Active source/tests/tools/templates | `bundle://proof/SB10/scans/old-observation-service-scan.txt`. |
| SB10-INV-010: SB10 projection code avoids the listed .NET performance antipatterns. | `repo://codex/bundles/process-module-architecture-v3/architecture/19-dotnet-performance-guardrails.md` | `bundle://proof/SB10/performance-scan-summary.json`. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Test / Scan |
| --- | --- | --- | --- | --- |
| `ProcessLiveProcessSnapshot` | Runtime projector | Live query service and future UI | Upserted from event stream; query filters old completed runs and preserves active runs. | Live window tests at `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:87` and `:104`. |
| `ProcessTimelineEventProjection` | Runtime projector | History query service and future timeline UI | Appended as ordered history records by global sequence and run id. | Replay test at `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:21`; projector mutation scan. |
| `ProcessRunDetailProjection` | Runtime projector | Run-detail query service and future detail UI | Upserted per run with incidents, manager messages, runtime canvas, artifact map, and freshness. | Source assertions plus focused replay test. |
| `ProcessProjectionFreshness` | Runtime projector and query aggregation | Live/history/detail consumers | Captures projection time, source event time, source sequence, projector name, and lag. | Freshness test at `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:124`. |
| Projection offsets | Replay worker | Replay worker and operations/replay tooling | Stored only after a successful projection batch item. | Dead-letter test at `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:52`. |
| Projection dead letters | Replay worker | Operations/replay tooling | Failed projection item is persisted with explicit error metadata and the offset is not advanced. | Dead-letter test and anti-stub audit. |
| Projection history rows | EF projection store | Projection query service | Persisted in `process_projection_history` independently from runtime state/event rows. | CodeAnalytics persistence query and process-specific UI/Application EF scan. |
| Restricted diagnostic references | Runtime projector | Future UI and diagnostic link resolvers | Normal read models carry reference ids only, not raw diagnostic payload text. | Restricted diagnostic test and raw diagnostic projection scan. |

## Semantic Adequacy Gate

| Gate item | Evidence |
| --- | --- |
| Shallow-pass trap | A fake projection layer could directly query runtime EF rows or rebuild `ProcessObservationService` while making the UI appear live. |
| Adversarial negative proof | Projector mutation scan has no runtime mutation/store/DbContext matches; process-specific UI/Application scan has no Process runtime/persistence EF matches; restricted diagnostics test proves payload detail is not projected. |
| Semantic positive proof | Replay creates live snapshots, run detail snapshots, timeline history, freshness, and offset progression from runtime events. |
| Anti-stub audit | `bundle://proof/SB10/scans/anti-stub-scan.txt` reports no TODO, placeholder, stub, or `NotImplementedException` markers in SB10 source/tests. |
| Source assertions | `bundle://proof/SB10/source-assertions.txt`. |
| Failing-first proof | `bundle://proof/SB10/failing-first-process-projection-tests.txt`. |
| Passing proof | `bundle://proof/SB10/test-unit-sb10.txt`, `bundle://proof/SB10/test-unit-sb10-process-slice.txt`, `bundle://proof/SB10/build-unit-sb10.txt`, and `bundle://proof/SB10/build-solution-sb10.txt`. |
| CodeAnalytics proof | `bundle://proof/SB10/codeanalytics-snapshot-summary.txt`. |

## Validation Commands

```text
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "ProcessProjectionPipelineTests" --nologo
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "ProcessProjectionPipelineTests|ProcessManagerControlLoopTests|ProcessPersistenceStoreTests|ProcessRuntimeEngineTests|ProcessInstancePlanCompilerTests|ProcessDriverAbstractionTests|ProcessTemplateGitFoundationTests|ProcessCoreKernelTests|ProcessModuleBoundaryTests" --nologo
dotnet build tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --nologo
dotnet build CanDoItAll.slnx --nologo
```

Results are captured in `bundle://proof/SB10/test-unit-sb10.txt`, `bundle://proof/SB10/test-unit-sb10-process-slice.txt`, `bundle://proof/SB10/build-unit-sb10.txt`, and `bundle://proof/SB10/build-solution-sb10.txt`.
