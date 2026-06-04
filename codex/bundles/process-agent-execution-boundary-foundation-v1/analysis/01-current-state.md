# Current State

## Completed Boundary Work

The previous provider hardening phase moved product runtime tool attachment out of hard-coded MAF paths and into first-party `IAgentRuntimeToolProvider` contributors. `CanDoItAll.AgentFramework.Maf` now references `CanDoItAll.AgentFramework.Tooling` but no longer references Processes, Projects, or Workbench directly.

Current first-party runtime providers:

| Provider | Owning module | Descriptor key | Tool surface |
| --- | --- | --- | --- |
| Processes | `CanDoItAll.Modules.Processes` | `processes.runtime-tools` | 23 process tools |
| Project Structure | `CanDoItAll.Modules.Workbench` | `project-structure.runtime-tools` | 28 project-structure tools |
| Image Generation | `CanDoItAll.Modules.AgentFramework` | `image-generation.runtime-tools` | `image_generation_create` |

## Remaining Process Coupling

`ProcessRunAutomationDispatchService` still directly depends on AgentFramework execution abstractions and types. In `ProcessRunAutomationDispatchService.cs` the constructor injects `IAiTechnicalAgentBridge` and `IAgentFrameworkWorkspaceService`. In `ProcessRunAutomationDispatchService.Execution.cs`, the dispatcher directly calls `workspaceService.ExecuteRunAsync`, `workspaceService.GetExecutionRunDetailAsync`, and catches AgentFramework-specific exceptions.

This means the process runtime has not yet been made ready for a clean core split. A full Process Core extraction would require either dragging AgentFramework dependencies into the core or doing a very large one-shot rewrite.

## Recommended Current Position

The branch is ready for the next preparatory seam, not for the full core split.

Next seam: isolate agent execution behind a process automation execution client/facade and begin minimal contracts/abstractions foundation.
