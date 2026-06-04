# Current State

The branch has completed the execution-boundary and first artifact-boundary passes. The current production shape is intentionally still inside `CanDoItAll.Modules.Processes`.

## Completed Foundations

- Dispatcher execution calls are behind `IProcessAutomationExecutionClient`.
- Process-owned execution snapshots exist in `CanDoItAll.Processes.Contracts`.
- Execution detail/result/failure/receipt observation are no longer the blocking boundary.
- First artifact helpers exist:
  - `ProcessArtifactExpectationMatcher`
  - `ProcessArtifactProjectionLineageBuilder`
  - `ProcessArtifactProjectionPlanner`
  - `ProcessArtifactEvidenceValidationRules`
- The execution-artifact projection path now calls `ProcessArtifactProjectionPlanner.PlanExecutionArtifact`.

## Remaining Hotspots

- `ArtifactProjection.cs` still owns many source-specific projection paths and side effects.
- `ArtifactValidation.cs` still mixes evidence rules, textual proof validation, browser proof interpretation, and project-structure requirement weakening detection.
- Some helpers still depend on `ProcessRunAutomationDispatchService` nested types, making later movement harder.
- Storage placement and DB artifact recording are still inline in the dispatcher orchestration.
