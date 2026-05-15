# 03-process-live-workflow-tabs-and-dialogs

## Status

- `Completed`

## Objective

- Redesign the dense tab bodies and dialog families for process workspace, live process dashboard, and workflow runtime pages after the tree/detail foundations are available.

## Covered Inputs

- RN-001 improve visual look, working space, and clarity.
- RN-007 use maximum available desktop width.
- RN-008 design proposals for each tab content and dialogs.
- RN-009 use BaseLib/Tailwind/shared components.
- RN-010 move excessive information into dialogs.
- RN-011 tree/list management for process and workflow surfaces.
- RN-012 B2B customer-video readiness.

## Prerequisites

- SB00-01 page inputs and accepted proposals passed.
- SB00-03 BaseLib tree/detail/tab/dialog primitives passed or explicit exception recorded.
- SB03 tree-driven project/process/workflow surface work has established process/workflow selection behavior.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\02-processes-live.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\03-agents-workflows.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\03-process-pages-tabs-dialogs-proposal.png`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\04-agent-workflow-tabs-dialogs-proposal.png`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\LiveProcessesDashboard.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`

## Deliverables

- Process `Definition`, `Roles`, `Steps`, `Runs`, `Analytics`, `Exchange`, and `Manager chat` tab bodies use reusable dense tab workspace pattern.
- Runs nested tabs `Launch`, `Activity`, `Control`, `Execution`, `Coordination`, `Evidence` keep selected run context visible.
- Process role, canvas action, choose-run, run-steps, and template dialogs use reusable dialog scaffold where touched.
- Live process `Activity`, `Agents`, `Graphs`, `Tool analytics`, plus run detail `Overview`, `Steps`, `Artifacts`, `Timeline` surfaces are compact and dialog-driven.
- Workflows `Dashboard`, `Workflows`, `Editor`, `Templates`, `History`, `Analytics` tab bodies use tree/list/detail and dense layouts.
- Workflow preview inputs, run detail, event detail, and canvas editor dialogs follow inspector dialog patterns.

## Dependency Impact

- SB06 final proof depends on these high-visibility customer-video routes.
- Failures here may reopen SB00-03 if generic tab/dialog primitives cannot carry the real content.

## Validation Depth

- High-risk UI interaction and dialog proof.

## Implementation Steps

1. Work from the page inputs and accepted proposal panels.
2. Preserve all existing process runtime, role, import/export, manager chat, live dashboard, and workflow runtime actions.
3. Convert tab bodies one by one to dense shared patterns; verify no tab loses its selected entity context.
4. Move low-frequency or payload-heavy data into dialogs/inspectors.
5. Use component `Class` parameters, shared BaseLib variants, and Tailwind utilities only.
6. Add or update tests for moved tab/dialog interactions.
7. Capture large-screen screenshots for every changed tab body and open dialog.

## Scope Exceptions

- Do not rewrite process or workflow domain services unless needed for typed read models.
- Do not replace canvas editors; only improve surrounding density and dialogs.
- Do not tune mobile/medium layouts.

## Do Not Do

- Do not hide runtime controls behind ambiguous menus.
- Do not remove import/export, publish, manager chat, approval, or evidence actions.
- Do not add new page-local CSS.

## Acceptance Checklist

- Every listed process/workflow/live tab body has a proposal-backed implementation or explicit exception.
- Every changed dialog has open-state proof.
- Selected process/run/workflow context remains visible.
- Targeted tests cover moved interactions.
- Large-screen screenshots show no overlap, clipping, or excessive gutters.

## Proof Required

- Targeted unit/component/Playwright tests for moved interactions.
- Screenshots for process tabs, runs nested tabs, live process tabs, workflow tabs, and open dialogs.
- Execution report rows updated for all changed tab/dialog states.
- Diff review for no new page-local CSS.

## Browser Validation Logging

- Routes: `/processes`, `/projects/{ProjectId}/processes`, `/processes/live`, `/projects/{ProjectId}/processes/live`, `/agents/workflows`.
- Viewport: large desktop, recommended `1920x1080`.
- Actions: select tree/list item, switch every tab, switch nested runs tabs, open role/action/choose-run/template/run/event dialogs, operate workflow preview.
- Screenshots: each tab group plus each dialog open state.
- Review questions: is the main work context visible, are actions reachable, are dense details readable, and does it resemble the Economy operations density.

## Progression Gate

- Final visual proof cannot start until all changed process/live/workflow tab and dialog states have screenshot proof or explicit blockers.

## Suggested Agent Prompt

```text
Implement subbundle 03-04 only. Redesign process, live process, and workflow tab bodies and dialogs using the shared dense tab and inspector dialog patterns. Preserve every existing action, use no page-local CSS, run targeted tests, capture large-screen screenshots for each tab/dialog state, and update the execution report.
```
