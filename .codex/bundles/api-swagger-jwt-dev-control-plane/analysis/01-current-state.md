# Current State

## Repository Findings

- Host app: `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs` is a .NET 10 Blazor Server host using minimal endpoints for development diagnostics and `MapProjectStructureAgentApi()`.
- Current project-structure HTTP surface: `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs` already maps `/api/project-structure-mcp` routes and calls `ProjectStructureAgentService`, `ProjectStructureLeaseService`, and `ProjectStructureAnalyticsService`.
- Project CRUD and hierarchy source of truth: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs` contains `ProjectsService`.
- Process source of truth: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.cs` and its partial files expose definition, runtime, launch, HR matching, direct messaging, artifact, assignment, and analytics operations.
- Process MCP wrapper: `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Processes\ProcessesCoordinator.cs` proves the useful API surface and error mapping but should not be copied into `CanDoItAll.Web`.
- Agent source of truth: `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs` defines `IAgentFrameworkWorkspaceService`; `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Workspace\CurrentProfileAgentFrameworkWorkspaceService.cs` forwards to the current profile-backed workspace and synchronizes directory projection on agent mutations.
- Settings UI: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor` and `.razor.cs` use BaseLib components and secondary tabs. New UI should follow this pattern.
- OpenAPI precedent: `C:\repositories\CanDoItAll\tools\CanDoItAll.Manager\Program.cs` uses `Microsoft.AspNetCore.OpenApi` with `AddOpenApi()` and `MapOpenApi()`.

## Existing Flows

- UI, MCP, and agent tools already share `ProcessesService` for process behavior. The new API should call the service directly, not call MCP tool wrappers and not duplicate persistence logic.
- Project structure has a stronger central API already. The new work should keep it alive, document it, and add auth/OpenAPI infrastructure rather than rewriting every operation under a new path.
- Process launch planning already includes project-structure context and HR matching through `CreateLaunchPlanAsync`, `MatchLaunchPlanWithHrManagerAsync`, `SelectLaunchCandidateAsync`, approval, provisioning, and execution methods.
- Agents have broad catalog/execution APIs through `IAgentFrameworkWorkspaceService`; endpoint handlers can remain thin wrappers.
