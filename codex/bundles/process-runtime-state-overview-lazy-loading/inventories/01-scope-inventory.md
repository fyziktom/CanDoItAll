# Scope Inventory

## Code Surfaces

| Surface | Files |
| --- | --- |
| Process workspace shell and badges | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs` |
| Workspace loading and refresh | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.LiveRefresh.cs` |
| Run details and active summaries | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs` |
| Runs tab/list UI | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsTab.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsActiveSection.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsLifecycleSection.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RunsPresenter.cs` |
| Runtime service/read models | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeViewModels.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.Reads.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesModuleServiceCollectionExtensions.cs` |
| Runtime mutations | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.Operations.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.StepTransitions.cs` |
| Tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`, `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureProcesses.cs` |

## Data States

- Active: `ProcessRunStatus.Active`
- Blocked: `ProcessRunStatus.Blocked`
- Failed: `ProcessRunStatus.Failed`
- Stopped: `ProcessRunStatus.Cancelled`

## Commands

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter ProcessesServiceIntegrationTests`
- `dotnet build CanDoItAll.slnx`
- Browser pass against `https://localhost:7271/processes` or project-scoped processes route when local app is available.
