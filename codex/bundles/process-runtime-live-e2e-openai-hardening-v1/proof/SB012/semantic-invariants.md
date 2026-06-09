# SB012 Semantic Invariants

## Shallow-Pass Trap
SB012 is not passed by merely opening a process workspace URL. A route shell can render while the run is not selected, while the route omits `processId`, or while the popup is paused behind startup confirmation.

The rejected adversarial proof is captured in:
- `proof/SB012/red-team/shallow-route-only-proof.md`
- `proof/SB012/red-team/shallow-route-only-rejection.txt`

## Required Positive Invariants
- Project-structure start is executed with `Execute: true` and returns `run-started`.
- Persisted run trigger parses through `ProcessProjectStructureContextFormatter.TryParse`.
- Persisted context contains `ProjectId`, `NodeId`, `ParentNodeId`, and `ParentNodeTitle`.
- Run readback returns the same `ProjectId` and `ProcessDefinitionId`.
- Observation dashboard readback returns the same run for `{ projectId, processDefinitionId }`.
- Project-structure projection contains `process-run:{runId}`.
- Project-structure projection contains `process-run-output:*` under `process-run:{runId}` with `ArtifactKind == "process-run-output-folder"`.
- Projected process-run and output-folder routes include both `processId={definitionId}` and `runId={runId}`.
- `/projects/{projectId}/processes` receives `processId`, `runId`, and `launchPlanId` through routable pages and passes them into `ProcessWorkspace`.
- Large-desktop browser proof opens the projected output folder quick action and verifies `processes-run-history-item-{runId}` plus `processes-selected-run-summary`.

## Negative Proof Result
The route-only proof was rejected because it omitted persisted bridge context, output-folder projection, route completeness, and selected-run UI proof. SB012 closure requires all positive invariants above.
