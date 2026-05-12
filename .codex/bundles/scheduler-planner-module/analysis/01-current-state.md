# Current State

## Solution Snapshot

- CodeAnalytics snapshot: `snap-20260415205622-d225a84b`
- Solution: `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Snapshot coverage: 61 projects, 1431 documents, no blocking analysis errors.

## Existing Automation Runtime

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Services\AutomationModuleServiceCollectionExtensions.cs`
  - Registers Quartz and hosted Quartz service.
  - Registers `IAutomationTriggerRegistry`, `QuartzAutomationSchedulerBridge`, message publishing, dispatch, telemetry, and background workers.
  - Current Quartz registration only sets scheduler id/name. No database-backed Quartz persistent store configuration was found.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Triggers\AutomationTriggering.cs`
  - `AutomationTriggerRegistry.SaveAsync` validates and stores `AutomationTriggerRecord`, then synchronizes Quartz.
  - `QuartzAutomationSchedulerBridge` projects canonical DB trigger rows into Quartz jobs/triggers.
  - `AutomationTriggerQuartzJob` publishes durable `AutomationTriggerFireRequest` and updates last/next fire state.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Runtime\AutomationRuntimeModels.cs`
  - `AutomationTriggerRecord` maps to `Automation_Triggers`.
  - `AutomationTriggerKind` already supports `Cron`.
  - `AutomationTriggerMisfirePolicy` already supports `FireOnceNow`, `DoNothing`, and `IgnoreMisfire`.
  - `AutomationTriggerOwnerKind` currently has `Platform`, `Module`, `Plugin`, `Project`, `Agent`; no explicit workflow/process scheduler target.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Runtime\AutomationRuntimeContracts.cs`
  - `IAutomationTriggerRegistry` exposes save/get/list.
  - `AutomationTriggerFireRequest` is the durable fire message contract.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Services\AutomationMessagingServices.cs`
  - Durable message envelopes, delivery attempts, retries, and dead-letter state already exist.
  - No specific workflow/process scheduled-fire handler was found.

## Existing Persistence

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContext.cs`
  - Uses EF configuration discovery and registered module assemblies.
  - Relational profiles are migrated through the runtime bootstrapper.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
  - Runtime modules are added explicitly.
  - New module work must register services here or through an equivalent composition extension.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs`
  - Module assemblies are listed explicitly for EF model discovery and Blazor routing.

## Existing Process And Workflow Run Surfaces

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.RunStart.cs`
  - `ProcessesService.StartRunAsync(ProcessRunStartRequest)` starts process runs and records process run state.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
  - `IAgentFrameworkWorkspaceService.ExecuteRunAsync(ExecutionRunRequest)` is the agent/workflow execution entry point.
  - Execution run reads are exposed through `ListExecutionRunsAsync`, `GetExecutionRunDetailAsync`, and related history methods.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.Helpers.cs`
  - Execution records already include `SchedulerRunId` through the invocation context. This is an important correlation hook for scheduled workflow runs.

## Existing UI Patterns

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Pages\AutomationPage.razor`
  - Uses `PageScaffold`, `PageHeader`, `SummaryTiles`, `SectionCard`, `FilterBar`, `Button`, `Stack`, `StatusBadge`, and `EmptyState`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
  - Uses tabbed workspace patterns and dense operational controls.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
  - Main navigation currently includes `Automation` at `/automation`, but no scheduler/planner item.

## Existing Test Surfaces

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AutomationRuntimeIntegrationTests.cs`
  - Existing tests already cover trigger persistence, Quartz rehydration, durable trigger-fire publication, one-shot retirement, and next-fire reload.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`
  - Existing component tests cover process/workflow UI patterns and can host Scheduler/Planner component tests.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`
  - Existing browser-proof project should own end-to-end tab and layout proof.

## Gaps To Close

- Quartz currently appears to run from in-memory scheduler state plus canonical trigger rehydration. The architect asked specifically for DB-backed Quartz recovery, so this must be treated as an implementation gate.
- There is no product-facing Scheduler/Planner module with typed workflow/process targets.
- There is no stored CRON description field/service.
- There is no schedule fire/run history entity that joins schedule, target, Quartz fire, durable message, and workflow/process run outcome.
- There is no handler that turns `AutomationTriggerFireRequest` into a workflow/process run.
