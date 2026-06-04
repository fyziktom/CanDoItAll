# Current State

## Last Completed Bundle

The previous artifact validation rule bundle completed successfully:

- Final red-team result passed.
- `ArtifactValidation.cs` was reduced from 3931 to 3223 lines.
- Rule helpers stayed local to `CanDoItAll.Modules.Processes`.
- No Process Core, driver-pack, MAF/Tooling product dependency, UI change, or prohibited viewport proof was introduced.

## Current Positive Boundaries

- MAF no longer owns first-party product tool construction.
- Process automation execution uses process-owned snapshots.
- Artifact projection writes are coordinator-owned.
- Artifact validation now has local snapshots and rule helpers for path/text/provider-native/quality/project-structure families.

## Remaining Dispatch Hotspot

`ProcessRunAutomationDispatchService.ToolValidation.cs` still mixes:

- required-tool discovery,
- required-tool missing detection,
- metadata-required tools,
- process mock satisfied tool substitution,
- carried implementation proof tool satisfaction,
- critical failure grouping,
- stack-inapplicable .NET failure suppression,
- completion status calculation,
- completion reason construction,
- declared outcome recovery,
- provider failure interaction,
- retry/rework interaction.

This is too much policy inside one dispatcher partial, but still not suitable for Process Core because it reads dispatcher candidate state and execution detail snapshots.

## Current Recommendation

Use another module-local boundary bundle:

- create typed tool-validation snapshots and fact collectors,
- extract pure rules into Processes-owned helpers,
- migrate the dispatcher through wrappers one area at a time,
- keep orchestration and side effects in dispatcher,
- document driver-readiness semantics without creating drivers.
