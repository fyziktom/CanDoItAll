# C# Current-State Inventory

## Source files inspected

- `src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- all `src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.*.cs` partials
- `src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureGanttTaskEditCoordinator.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskDetailsService.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskCreationService.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskResourceCostService.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkItemAssigneeService.cs`
- `src/Modules/CanDoItAll.Modules.CrmHr/Services/ProjectPartyAssignmentInvariantPolicy.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/Hr/HrAgentUsageAnalyticsService.cs`
- affected `.csproj`, DI registrations, component tests, and `docs/architecture/project-planning-analytics-and-agent-access.md`

## Responsibility inventory

| Source/member | Responsibility | Dependencies used | Target owner | Test seam | Risk |
| --- | --- | --- | --- | --- | --- |
| `ProjectStructurePage.ComponentAdapters.ResolveTaskAssignee` | Collapse canonical assignment rows into editor selection | assignment DTOs | `ProjectStructureTaskAssigneeResolver` | pure direct test | high |
| `ProjectStructureGanttTaskEditCoordinator.ResolveAssignee` | Same mapping plus conflict rejection | assignment DTOs | same resolver | pure direct test and coordinator smoke | high |
| `ProjectStructureTaskDetailsService.LoadCurrentAssigneeAsync` | Fetch/map/snapshot one assignment for scalar replacement | CRM bridge | retain single-assignment compensation and mixed-set replacement guard | mixed unchanged-save plus existing compensation tests | critical |
| `ProjectStructureTaskResourceCostService` person/agent branch | CRM workforce rate calculation | CRM rate bridge | person strategy; agent is removed from this branch | direct strategy tests | high |
| same service workflow branch | bounded completed-run usage estimate | workflow stores | workflow strategy | direct fake-store test | medium |
| same service process branch | historical process estimate | process services | process strategy | direct fake-reader test | medium |
| task create/detail/page submission paths | persist caller-provided cost | metadata/mutation services | estimate refresh service plus thin callers | isolated policy and integration tests | critical |

## Concentration and dependencies

- The page is an approximately 11k-line partial cluster with 29 injected dependencies; file splitting does not create responsibility isolation.
- `ProjectStructureTaskResourceCostService` has six constructor dependencies and four resource algorithms.
- `ProjectStructureGanttTaskEditCoordinator` has seven dependencies and currently contains mapping policy in addition to orchestration.
- `ProjectStructureTaskDetailsService` has five dependencies and snapshots only one assignment.
- `ProjectStructureTaskCreationService` has five dependencies and trusts submitted expected cost.

## Direct instantiation and composition

- Workbench services are registered in `WorkbenchModuleServiceCollectionExtensions`.
- `ProjectStructureGanttTaskEditCoordinator` is registered directly in `src/App/CanDoItAll.Web/Program.cs`.
- AgentFramework already references Workbench; Workbench does not reference the AgentFramework module.
- No new project reference is planned.

## Current tests

- CRM hour/man-day price calculations, workflow history, process estimate reader behavior.
- UI estimator caching and stale-result guards.
- task create compensation and row ordering.
- task details stale mutation and one-assignment compensation.
- canonical Gantt projection already proves a task may have person and AI-agent assignments simultaneously.
- metadata has no authoritative task-execution lifecycle; schedule, free-text status, and progress are not safe occurrence evidence.

## Missing tests

- direct assignment-resolution tests for multiple valid assignees.
- mixed-assignment unchanged-save preservation and direct-mutation blocking.
- strategy registry missing/duplicate registration negatives.
- Agent strategy cost-history behavior.
- commit-time repricing for new/explicitly `NotStarted` tasks, `Unknown` fail-closed behavior, and historical-state preservation.
- missing-price clearing instead of stale manual preservation.
- Gantt coordinator/dialog opening for mixed assignments.

## Evidence limitation

CodeAnalytics MCP is unavailable. The architecture gate substitutes exact file reads, `rg` symbol/reference searches, `.csproj` inspection, compiler validation, line/member counts, and targeted tests. A negative CodeAnalytics claim is not made.
