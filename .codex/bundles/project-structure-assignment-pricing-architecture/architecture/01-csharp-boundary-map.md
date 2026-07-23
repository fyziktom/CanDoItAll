# C# Boundary Map

## Target owners

| Type | Project | Responsibility |
| --- | --- | --- |
| `ProjectStructureTaskAssigneeResolution` | Workbench | immutable scalar display selection, multiplicity/ambiguity, and direct-mutation capability |
| `ProjectStructureTaskAssigneeSelectionPolicy` | Workbench | deterministic interpretation of valid person/agent assignments |
| `IProjectStructureTaskResourceCostStrategy` | Workbench | one resource-kind pricing contract |
| `ProjectStructureTaskResourceCostService` | Workbench | validate request and dispatch to exactly one strategy |
| `ProjectStructurePersonTaskResourceCostStrategy` | Workbench | CRM workforce-rate quote |
| `ProjectStructureWorkflowTaskResourceCostStrategy` | Workbench | bounded workflow-run-history quote |
| `ProjectStructureProcessTaskResourceCostStrategy` | Workbench | process historical-cost quote |
| `ProjectStructureAgentTaskResourceCostStrategy` | AgentFramework | agent execution-usage/history quote |
| `ProjectStructureTaskEstimateRefreshService` | Workbench | lifecycle-aware authoritative quote application/clearing |
| `ProjectTaskExecutionStatePolicy` | Workbench | validate explicit execution state and forward-only transitions |
| `ProjectStructureCanvasTaskDialogCoordinator` | Workbench | canvas task create/edit dialog orchestration outside the page partial |
| `ProjectStructureTaskApplicationService` | Workbench | shared create/edit saga, revision validation, quote application, callback boundary, and compensation |
| `ProjectStructureWorkItemAssignmentRevisionService` | Workbench | transaction-local WorkItem display/revision synchronization staged through Projects contracts |

## Contracts versus implementations

- The strategy interface and quote/request records stay in Workbench because Project Structure owns the use case.
- Person/workflow/process implementations stay in Workbench because their dependencies are already legal there.
- Agent implementation stays in AgentFramework, which already depends on Workbench and owns agent usage analytics.
- No interface is created for the pure assignment policy; it has one stable implementation and is directly testable.

## Composition root responsibilities

- Workbench registration owns the dispatcher, assignment/lifecycle policies, refresh service, canvas/Gantt coordinators, and person/workflow/process strategies.
- AgentFramework registration contributes the agent strategy.
- The Web composition root continues to resolve the Gantt coordinator; module registration may absorb it only if that does not change host behavior.
- Missing or duplicate strategies produce an explicit exception; there is no default strategy.

## Old responsibilities removed

- resource-kind algorithms and external dependencies leave `ProjectStructureTaskResourceCostService`.
- assignment-count rejection/mapping leaves the Gantt coordinator and `ProjectStructurePage.ComponentAdapters`.
- mixed-assignment rejection leaves UI preparation; the details-service guard remains as defense in depth.
- callers no longer decide whether a stale/manual cost is authoritative for an unstarted task.
- canvas task dialog orchestration leaves the page partial.
- direct-assignment revision and pricing compensation leave Gantt/canvas coordinators and are shared by the application saga.

## Responsibilities deliberately left

- `ProjectStructureGanttTaskEditCoordinator` remains the dialog/orchestration owner.
- `ProjectStructureTaskDetailsService` remains the single-assignment transaction/compensation owner and mixed-set replacement guard.
- `ProjectStructurePage.ComponentAdapters` retains only thin component event delegation and unrelated adapters.
- Whole-page workflow/process extraction is follow-up scope, not a hidden part of this bundle.
- Existing non-task WorkItems retain direct-assignee support; task-specific pricing cleanup is not applied to them.

## Partial-class prevention

No new partial is allowed. New pricing resource kinds are added through a top-level strategy and DI registration. New assignment-selection policy belongs in the resolver. The source assertion at closure checks that duplicate `ResolveAssignee`/`ResolveTaskAssignee` policy does not reappear in page partials/coordinators.
