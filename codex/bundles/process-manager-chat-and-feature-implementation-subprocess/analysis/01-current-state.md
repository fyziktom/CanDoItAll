# Current State

- Process detail tabs are rendered in `ProcessWorkspace.razor`; `Exchange` is currently the last tab.
- `ProcessWorkspace` already injects `IAgentFrameworkWorkspaceService` for approval continuation, and the process module references `CanDoItAll.AgentFramework.Components`.
- `ChatWorkspacePanel` is the existing standard chat surface used by contextual agent windows.
- Process runs expose `ManagerAgentId` and `ManagerAgentName`; manager directory options expose both `PartyId` and `TechnicalAgentId`.
- The previous subprocess implementation created parent-child run state and default .NET development subprocess templates.
- The template pack already has a .NET implementation slice subprocess used by the main software-delivery process.

## Revalidation Notes

- The manager chat must not create process-specific chat tables. AgentFramework chat remains canonical.
- The selected process run is prompt and invocation context, not a new durable chat identity.
- The feature/function subprocess should add granularity without hard-coding .NET-specific dispatcher behavior.
