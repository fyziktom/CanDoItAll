# 03-tree-driven-project-process-and-workflow-surfaces

## Status

- `Completed`

## Objective

- Convert the highest-impact hierarchical workspaces to tree-driven navigation: projects, processes, workflows, and related large lists where hierarchy or grouping is already present.

## Covered Inputs

- RN-001 improve clarity and working space.
- RN-007 use maximum available width.
- RN-010 use dialogs when pages contain too much information.
- RN-011 use treeview for projects, processes, workflows, and larger lists.
- RN-009 use Tailwind/BaseLib/shared component improvements, not own CSS.

## Prerequisites

- SB00-03 tree/detail/tab/dialog primitives passed.
- SB01 route inventory and baseline complete.
- SB02 shell foundation passed.
- Existing project/process/workflow list behavior has been reviewed before changing interaction structure.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TreeView.razor`
- `C:\repositories\CanDoItAll\Tailwind\navigation\treeview.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectHierarchyModal.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\LiveProcessesDashboard.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll.Economy\src\CanDoItAll.Economy.Simulator.Components\Components\BusinessUnitTree.razor`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\02-project-pages-tabs-dialogs-proposal.png`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\03-process-pages-tabs-dialogs-proposal.png`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\04-agent-workflow-tabs-dialogs-proposal.png`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureProcesses.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs`

## Deliverables

- Project tree/list-detail surface for portfolio and hierarchy navigation.
- Process definition tree grouped by global/project scope, status, subprocess relationship, and run health where data exists.
- Workflow tree grouped by definition, version, lifecycle status, components, and recent runs.
- Shared tree/detail implementation uses SB00-03 primitives instead of page-local CSS.
- Detail panes or dialogs for dense secondary metadata.
- Typed tree node builders/adapters with unit or component tests.
- Updated Playwright smoke tests for project/process/workflow navigation.

## Dependency Impact

- SB04 and SB06 depend on the tree patterns as the reference approach for other large-list page repairs.
- Weak tree adapters risk breaking navigation and selection across project/process/workflow workflows.

## Validation Depth

- Critical UI foundation for hierarchical workspaces.

## Implementation Steps

1. Model project tree nodes from `ProjectSummary` and hierarchy links with typed ids and explicit node kinds.
2. Replace or supplement the Projects board with a tree/detail view while preserving modal create/edit flows.
3. Model process tree nodes from process definitions, project scope, status, active run counts, and subprocess relationships.
4. Update `ProcessWorkspace` list pane to use `TreeView` or a shared tree/list-detail component.
5. Model workflow tree nodes from workflow definitions, versions, lifecycle status, components, and recent runs.
6. Move verbose secondary information from list rows into detail panes or dialogs.
7. Add component/unit tests for typed node builders and route selection behavior.
8. Update Playwright smoke tests for tree selection, expansion, badges, empty states, and detail navigation.

## Scope Exceptions

- If a target list has no real hierarchy or useful grouping, keep a compact list and document the exception in the route inventory.
- Do not replace canvas-heavy project structure behavior unless the tree is only a navigation/support panel.

## Do Not Do

- Do not create tree nodes with magic string commands hidden in UI markup.
- Do not lose existing create/edit/import/export/runtime actions.
- Do not add new page-local CSS for tree layout.
- Do not rewrite process or workflow domain services unless needed for typed read models.

## Acceptance Checklist

- Projects show a useful TreeView or tree/detail surface.
- Processes show a useful TreeView grouped by scope/status/project or explicit exception.
- Workflows show a useful TreeView grouped by definition/version/status or explicit exception.
- Selection, expansion, badges, empty states, and detail panes work on large desktop.
- Dense metadata is reachable through dialogs/flyouts/detail panes.
- Tests cover tree builder behavior and at least one browser flow per surface.

## Proof Required

- Targeted unit/component tests for tree node builders.
- Playwright screenshots for `/projects`, `/processes`, `/projects/{id}/processes`, and `/agents/workflows`.
- Open-state screenshots for any dialogs or context menus introduced.
- Diff review proving no new page-local custom CSS.

## Browser Validation Logging

- Routes: `/projects`, `/processes`, `/projects/{ProjectId}/processes`, `/agents/workflows`, and a representative `/projects/{ProjectId}/structure` support-panel path.
- Viewport: large desktop, recommended `1920x1080`.
- Actions: expand/collapse tree nodes, select branch and leaf nodes, trigger detail action, open any dialog, verify selected state persists.
- Screenshots: tree default state, selected node/detail, expanded grouped state, dense detail dialog.
- Review questions: does tree grouping make management clearer, does it save working space, are labels minimal, are badges useful, and are details still reachable.

## Progression Gate

- Downstream page density work may continue only after projects, processes, and workflows have tree proof or explicit exceptions recorded with rationale.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Add tree-driven project, process, and workflow surfaces using BaseLib TreeView and typed node builders. Preserve existing actions, keep dense details reachable in dialogs or detail panes, avoid new custom CSS, run targeted tests, capture large-screen screenshots, and update the execution report.
```
