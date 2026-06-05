# SB11 Source Assertions

- Added `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs` as the module-local route planner named in the target architecture.
- The planner returns explicit `ProcessDispatchRouteKind` decisions for database requirement, upstream materialization, stranded recovery, subprocess, workflow, and agent execution.
- The planner only consumes already-known booleans and `ProcessDispatchRouteSnapshot` facts. It does not call EF, workflow, subprocess, agent execution, finalizer, transition, storage, logging, or service-scope APIs.
- `DispatchAsync` still owns the side-effect methods: `BlockDispatchForDatabaseRequirementAsync`, `TryRequestMissingUpstreamArtifactMaterializationAsync`, `TryRecoverStrandedMissingCompletionArtifactsAsync`, `HandleSubprocessDispatchAsync`, `workflowRunCoordinator.TryRunOrObserveAsync`, and `ExecuteUntilSettledAsync`.
- Focused tests cover the six required route decisions and prove subprocess/workflow/agent execution classification separately from side effects.
- No Process Core, production process driver API, UI, or viewport proof artifacts were introduced.
