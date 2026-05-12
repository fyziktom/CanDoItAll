# Scope Inventory

## In-Scope Existing Files

| Area | Files |
| --- | --- |
| Automation registration | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Services\AutomationModuleServiceCollectionExtensions.cs` |
| Automation trigger projection | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Triggers\AutomationTriggering.cs` |
| Automation contracts/models | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Runtime\AutomationRuntimeContracts.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Runtime\AutomationRuntimeModels.cs` |
| Automation messaging | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Services\AutomationMessagingServices.cs` |
| App DB | `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContext.cs` |
| Runtime composition | `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\CanDoItAll.Composition.csproj` |
| Web composition | `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`, `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Routes.razor` |
| Process launch | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.RunStart.cs` |
| Workflow execution | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` |
| Existing Automation UI | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Pages\AutomationPage.razor` |
| Existing workflow UI | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor` |
| Existing tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AutomationRuntimeIntegrationTests.cs`, `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`, `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright` |

## Expected New Files Or Areas

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\CanDoItAll.Modules.SchedulerPlanner.csproj`
- SchedulerPlanner domain models, EF configurations, migrations, service registration, application service, Automation message handler, adapters, and page/components.
- Integration tests under `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`.
- Component tests under `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`.
- Playwright proof under `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright` or existing browser validation harness.

## Out Of Scope

- Replacing Quartz.
- Redesigning the whole Automation page.
- Changing process/workflow domain models beyond the minimum scheduler correlation fields needed for clean linkage.
- Implementing code during this preparation bundle.
