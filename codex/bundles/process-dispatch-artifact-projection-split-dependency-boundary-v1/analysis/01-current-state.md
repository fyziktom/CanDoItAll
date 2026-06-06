# Current State Analysis

## What is stable enough

The recent projection coordinator bundle is a correct intermediate step. It proved that `ArtifactProjection.cs` can act as a source-family orchestration facade, and the proof says source-family order is preserved. The current `ProjectExecutionArtifactsAsync` method constructs a coordinator context and delegates to seven projection coordinators in order.

## Why this is still not ready for Process Core

The projection boundary is still nested inside `ProcessRunAutomationDispatchService`:

- `ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs` is a private nested implementation boundary, not a true module-local service boundary.
- Nested coordinators can call private dispatcher methods and static wrappers directly, which makes coupling invisible.
- Several projection-family coordinators still mix matching, file-system reads/copies, synthetic artifact construction, projection plan creation, write coordination, logging and candidate-state mutation.
- This is safer than the previous monolithic `ArtifactProjection.cs`, but it is not yet a stable foundation for Process Core extraction.

## Current major hotspots to keep watching

| Hotspot | Current state | Recommended handling |
| --- | --- | --- |
| `ArtifactProjectionCoordinators.cs` | Transitional large nested coordinator boundary | Split into one module-local file per coordinator family |
| `ArtifactProjection.cs` | Thin facade plus residual helpers | Keep as dispatcher adapter; reduce to orchestration and legacy compatibility wrappers only |
| Projection host dependencies | Hidden through nested private-method access | Introduce explicit internal host/services object with named operations |
| File-system side effects | Present in source-family coordinators | Keep explicit and named; do not hide in pure rules |
| Candidate state mutation | Centralized but nested | Move to top-level internal helper and test exactly |
| Future driver readiness | Still documentation-only | Keep documentation-only; no `IProcessDriverPack` yet |

## Non-blocking issues from prior reports

A broader architecture test class still has unrelated historical bundle fixture failures in some checkouts. This should be tracked as repository cleanup, but it must not block this runtime refactor unless the same tests fail for the new artifact-projection boundary behavior.