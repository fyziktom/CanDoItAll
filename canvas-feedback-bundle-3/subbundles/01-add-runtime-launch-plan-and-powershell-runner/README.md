# 01 Add Runtime Launch Plan And Powershell Runner

## Objective

Add the shared workbench service that decides whether a node can be launched and translates supported node metadata into a deterministic PowerShell launch plan.

## Covered Inputs

- `N001`
- `N002`
- `R002`
- `R003`
- `R004`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureLocalFileOpener.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\WorkbenchModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\WorkspaceStorage.cs`

## Deliverables

- a typed runtime-launch service registered in the workbench DI container
- deterministic launch-plan resolution for supported script and environment nodes
- Windows PowerShell process launch support for normal and elevated execution

## Implementation Steps

1. Introduce a dedicated workbench service for runtime launch resolution and execution.
2. Resolve workspace-relative paths against the current workspace root.
3. Build launch plans for supported script and environment nodes from existing typed metadata.
4. Return explicit failure reasons when metadata is incomplete or the node kind is unsupported.

## Do Not Do

- do not move runtime-launch logic into `ProjectStructurePage.razor`
- do not reuse the existing node-command routing path for local shell launch
- do not add best-effort fallback commands when required metadata is missing

## Acceptance Checklist

- supported dotnet watch nodes resolve a `dotnet watch` launch plan from node metadata
- supported python/script/runtime nodes resolve predictable commands and working directories
- unsupported nodes do not produce a launch plan
- the service can launch either normal or elevated PowerShell on Windows

## Proof Required

- focused automated tests for launch-plan resolution
- execution report updated with the exact validation command and result

## Suggested Agent Prompt

```text
Implement subbundle 01 only.

Add a dedicated workbench runtime-launch service that resolves supported script and environment nodes into deterministic PowerShell launch plans. Keep the logic strongly typed around the existing metadata model and fail explicitly when a node cannot be launched safely.
```
