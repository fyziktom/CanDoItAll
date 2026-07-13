# Current State

## Runtime Behavior

The runtime detects a false `Completed` state when required product content is missing, but the recovery path treats the resulting blocked outcome as manager-required. In the incident, the diagnostic was safe and idempotent, yet `ProcessRuntimeEngine` classified it as `Unknown` and selected manager escalation.

The adapter also short-circuits completion validation, so the first observed issue can hide a more actionable missing receipt. For the calculator incident, solution membership readback failed, but the missing `workspace_pwsh_run_script` receipt is the stronger repair signal because it identifies the deterministic wiring helper that never ran.

## Template Behavior

Several templates encode deterministic work as agent instructions instead of typed runtime contracts. The `dotnet-solution-setup` flow expects a helper script to wire the generated project into the solution, but the agent can report completion without the helper receipt. Similar proof-chain risks exist in subprocess parent templates, screenshot/writeback flows, Blazor delivery flows, and artifact templates that depend on semantic acceptance rather than physical file existence.

## Architecture Hotspots

- `AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` contains large validation and conversion logic.
- `AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs` owns product content checks.
- `AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs` owns required tool receipt checks.
- `AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs` currently maps completion issues toward manager-required behavior.
- `ProcessRuntimeEngine.ResultHelpers.cs` maps blocked results and classifies failure categories.
- `ProcessLaunchApplicationService.cs` enriches launch variables but does not centrally resolve tool-critical placeholders.
- `ProjectStructureProcessLaunchVariableContributor.cs` emits unresolved script refs for the .NET setup helper.
- `ParentSubprocessArtifactBridge.cs` skips stopped non-completed children and uses physical file existence as output evidence.
- `ProcessRuntimeToolPreflightService.cs` validates tool names but not exact args, paths, scopes, or side-effect manifests.
- `MafAgentRuntime.cs` validates structured finalizer shape, not process semantic gates.

## CodeAnalytics Evidence

The CodeAnalytics scoped snapshot found no dependency cycles in the scoped process projects, but it identified large classes and partial clusters around runtime integration. The implementation should extract cohesive services, avoid expanding the existing partial cluster, and preserve current dependency direction.
