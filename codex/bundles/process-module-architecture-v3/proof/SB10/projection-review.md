# SB10 Projection Review

## Status

Passed on 2026-06-15.

## Review Results

| Check | Result | Evidence |
| --- | --- | --- |
| Projectors derive rebuildable state from runtime events. | Passed | `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:6`, `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionWorker.cs:48`, `bundle://proof/SB10/test-unit-sb10.txt`. |
| Projectors do not mutate runtime state. | Passed | `bundle://proof/SB10/scans/projector-runtime-mutation-scan.txt` reports no runtime store, state, DbContext, SaveChanges, or runtime entity access in projector/query code. |
| UI-facing queries read projection snapshots/history only. | Passed | `repo://src/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:5`, `bundle://proof/SB10/scans/ui-application-process-runtime-ef-read-scan.txt`. |
| Live last-hour semantics are explicit. | Passed | Completed runs outside the window are excluded; active runs outside the window stay visible. See `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:87` and `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:104`. |
| Freshness and projector lag are projected. | Passed | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionReadModels.cs:11`, `repo://src/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs:172`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:124`. |
| Raw diagnostics stay restricted. | Passed | Projections store restricted diagnostic references only; test proves raw detail is not projected. See `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:144` and `bundle://proof/SB10/scans/raw-diagnostic-projection-scan.txt`. |
| Replay/dead-letter behavior is explicit. | Passed | `repo://src/CanDoItAll.Processes.Projections/ProcessProjectionWorker.cs:48`, `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs:52`. |
| Old observation service was not rebuilt. | Passed | `bundle://proof/SB10/scans/old-observation-service-scan.txt` reports no active old observation service/dashboard matches. |

## False Positive Review

`bundle://proof/SB10/scans/ui-application-runtime-ef-read-scan.txt` contains three broad-scan matches in `repo://src/CanDoItAll.Web/Program.cs` for generic EF Core logging setup. The process-specific scan at `bundle://proof/SB10/scans/ui-application-process-runtime-ef-read-scan.txt` reports no matches for Process persistence/runtime EF entities or runtime tables in UI/Web/Application.

## Residual Risk

SB10 creates projection contracts, projector workers, persistence rows, and application query services. Composition wiring and visible Blazor UI are intentionally deferred to downstream subbundles, with SB13 owning browser proof.
