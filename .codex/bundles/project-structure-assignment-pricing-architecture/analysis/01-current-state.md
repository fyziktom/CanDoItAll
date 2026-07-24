# Current State

## Repository

- Branch at preparation: `projectstructure-refactor`.
- Worktree before bundle creation: clean.
- Solution: `CanDoItAll.slnx`; target framework: `net10.0`.
- CodeAnalytics MCP was searched for but is not callable in this session.

## Assignment evidence

- `ProjectStructureGanttTaskEditCoordinator.ResolveAssignee` rejects `taskAssignments.Length > 1` and emits the exact reported message.
- `ProjectStructureTaskDetailsService.LoadCurrentAssigneeAsync` independently repeats the same rejection and snapshots only one assignment.
- `ProjectStructurePage.ComponentAdapters.ResolveTaskAssignee` repeats a third “exactly one” check for the canvas editor.
- `ProjectStructureWorkItemAssigneeService.ReplaceAsync` already captures/restores a list for its metadata-sync rollback, proving the canonical store can hold multiple records.
- `ProjectPartyAssignmentInvariantPolicy` permits a `WorkItemAssignee` to be either a person or AI agent and does not impose a one-row invariant.

## Pricing evidence

- `ProjectStructureTaskResourceCostService` owns person/agent CRM pricing plus workflow and process history in one switch-heavy class.
- Person and `Agent` currently share the CRM workforce-rate branch, contradicting the requested agent estimation behavior.
- `ProjectStructureTaskResourceCostEstimator` is lazy and its failure path intentionally preserves an existing manual cost.
- `ProjectStructureTaskCreationService` and `ProjectStructureTaskDetailsService` persist caller-provided estimate amounts without an authoritative refresh.
- Canvas create/edit in `ProjectStructurePage.ComponentAdapters.cs` composes metadata directly and also depends on the client estimate.
- Existing tests already cover CRM hour/man-day conversion, workflow history, process history, estimator caching, task creation compensation, and task detail compensation.

## Architecture evidence

- `ProjectStructurePage` spans the `.razor` file plus 23 partial `.cs` files.
- Largest members at preparation: `ProjectStructurePage.razor` 2,912 lines, `Processes.cs` 1,769, `Workflows.cs` 1,030, `SelectionPanel.cs` 767, `PartyIntegration.cs` 520, `NodeEditing.cs` 516, and `ComponentAdapters.cs` 426.
- The page/component area already has one useful top-level coordinator (`ProjectStructureGanttTaskEditCoordinator`), but assignment resolution remains duplicated across it, the application service, and the page partial.
- Workbench already references the abstractions needed by person/workflow/process estimators. `CanDoItAll.Modules.AgentFramework` already references Workbench, so an agent-history strategy can implement a Workbench contract without adding a reverse reference.
