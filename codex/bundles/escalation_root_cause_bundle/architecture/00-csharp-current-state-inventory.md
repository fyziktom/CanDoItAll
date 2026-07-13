# C# Current State Inventory

## Snapshot

- CodeAnalytics snapshot: `snap-20260708171537-b7255757`.
- Scoped documents loaded: 309.
- Scoped projects loaded: 10.
- Dependency cycles in scoped graph: none reported.

## Current Responsibilities

| Area | Current owner | Concern |
| --- | --- | --- |
| Completion result conversion | `CanDoItAll.Modules.Processes` adapter partials | Large partial cluster mixes validation ordering, diagnostics, managed artifact staging, and adapter result shaping. |
| Product path/content gates | `AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs` | Gate result has useful metadata but participates in first-failure flow. |
| Required tool receipt gates | `AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs` | Missing receipt can be hidden by earlier path/content failure. |
| Recovery decision | `CanDoItAll.Processes.Runtime` | Blocked outcomes currently collapse to manager-required behavior. |
| Launch variable enrichment | `CanDoItAll.Processes.Application` plus Workbench contributor | Placeholder resolution is not centralized or enforced for tool-critical variables. |
| Subprocess artifact bridge | `CanDoItAll.Processes.Runtime` / process integration | Child root cause and artifact slot truth are not carried strongly enough. |
| Template contracts | `CanDoItAll.Processes.Templates` and `Templates/Processes` | Hard gates are partly prose-driven and not always machine-readable. |
| Tool preflight | Process runtime tool preflight service | Checks tool name availability but not exact composed tool intent. |

## Hotspot Files

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ParentSubprocessArtifactBridge.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeToolPreflightService.cs`

## Partial Class Policy

The adapter already has a large partial cluster. New behavior should be extracted into small services or records that are invoked by the adapter. Adding more partial files is allowed only for a narrowly scoped adapter shim and must not become the primary design.
