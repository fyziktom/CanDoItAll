Reviewed branch: `fyziktom/CanDoItAll`, ref `maf-processes-refactor`.

Observed signals from current branch:

- `process-dispatch-artifact-projection-coordinator-boundary-v1` execution report marks SB01-SB56 as completed.
- Source scans report no production driver APIs, no UI/Razor/CSS/JS/TS changes, no forbidden viewport proof, and preserved projection source-family order.
- `ProcessArtifactProjectionOrchestrator` now creates seven projection source coordinators in the required order.
- `ProjectExecutionArtifactsAsync` now builds a projection context and delegates to the orchestrator.
- The new `IProcessArtifactProjectionHost` is a broad module-local interface with many methods and nested dispatcher aliases.
- `DispatcherArtifactProjectionHost` is a large adapter forwarding projection calls back into `ProcessRunAutomationDispatchService`.

Architectural conclusion: previous bundle is complete, but the next safe seam is projection host facet decomposition, not Process Core.
