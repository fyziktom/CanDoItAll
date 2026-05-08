# Scope Inventory

## In Scope

- Observation contract/read-model planning for:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components`
- Existing read/query services:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.Support.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeStateOverviewService.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`
- Current UI integration points:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.LiveRefresh.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsTab.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsActiveSection.razor`
- Runtime-side facts consumed by observation:
  - process runs and step runs
  - launch plans
  - outbox records and dead-letter state
  - AgentFramework execution runs and approvals
  - escalation journal and operator approvals
  - artifacts, decisions, work briefs, conformance observations

## Out Of Scope

- Building the complete new flexible dashboard UI during bundle preparation.
- Replacing the existing Processes page.
- Changing process execution progression, step dependency logic, outbox dispatch semantics, or agent-specific instructions.
- Adding a mutation API to the observation layer.
- Introducing Radzen into this module.
- Migrating all existing component markup in one phase.

## Impacted Tests

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessActiveRunSummaryPerformanceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRuntimeReadQueryServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRuntimeOperatorReadModelTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessExecutionRunDisplayProjectorTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`

## New Test Areas To Add During Implementation

- Observation snapshot service unit tests.
- Observation cache key/invalidation tests.
- Multi-process dashboard projection integration tests.
- Staleness/error-state tests.
- Virtualized/windowed UI component tests.
- AI observation-intent parser/handler tests with no runtime mutation.
