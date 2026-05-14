# Project-Structure UI Actions Dialogs And Selection Status

## Status

- `Completed`

## Objective

- Wire the Blazor project-structure canvas UI for add workflow, start workflow confirmation, context/inspector actions, and workflow status detail in the selection floating window.

## Success Criteria

- Eligible nodes expose `Add workflow`.
- Workflow nodes expose `Start`.
- Add dialog shows workflow selection and input preview.
- Start confirmation opens without resource matching/staffing.
- Selection panel shows run status and step count.

## Covered Inputs

- `N003`, `N005`, `N009`, `N010`, `N011`, `N014`, `N015`
- `R003`, `R004`, `R006`, `R008`

## Prerequisites

- Subbundle 03 closure gate has passed.
- Query the CanDoItAll components MCP before adding new structural UI markup.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeEditing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.OverlayStates.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructureSelectionPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructureSelectionPanel.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructureSelectionPanelModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureOverlayDialog.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs`

## Deliverables

- Context and inspector actions for add/start workflow.
- Add workflow dialog UI.
- Start workflow confirmation dialog UI.
- Selection panel workflow status details.
- Component tests and Playwright proof.

## Dependency Impact

- Final validation depends on real canvas usability. If overlays clip or hide input/status, the workflow may technically run but the user cannot operate it from the project-structure canvas.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Query components MCP for dialog/layout primitives and record chosen components in the execution report.
2. Add workflow actions to project-structure action catalog and inspector resolution.
3. Add dialog state and event handlers for add/start workflow.
4. Render add and start dialogs using existing overlay/dialog patterns.
5. Extend selection panel model/rendering with workflow status detail.
6. Add component tests.
7. Run Playwright open-state proof for desktop and narrow viewport.
8. Update execution report gate and browser analytics rows.

## Scope Exceptions

- This subbundle does not add new workflow examples or run all 20 scenarios.

## Closure Evidence

- Components MCP was queried for `ProjectStructureOverlayDialog`, `FormField`, and `SurfaceCard`; the MCP transport closed, so implementation used the existing local project-structure dialog and selection-panel patterns.
- Component proof passed: `ProjectStructureActionCatalogAdapterTests|ProjectStructurePageTests` (`53` tests).
- Unit proof passed: `ProjectStructureWorkflowNodeKeysTests|ProjectNodeKindRegistryTests|ProjectStructureNodeCatalogTests` (`8` tests).
- Integration proof passed: focused project-structure workflow create/preview/start/status/OpenAPI filter (`6` tests).
- Browser proof passed: `Project_structure_workflow_nodes_can_be_added_started_and_inspected_in_browser`.
- Screenshot proof captured:
  - `.codex/bundles/project-structure-workflow-runs/proof/browser/project-structure-add-workflow-desktop.png`
  - `.codex/bundles/project-structure-workflow-runs/proof/browser/project-structure-start-workflow-confirmation.png`
  - `.codex/bundles/project-structure-workflow-runs/proof/browser/project-structure-workflow-selection-status.png`
  - `.codex/bundles/project-structure-workflow-runs/proof/browser/project-structure-workflow-summary-mobile.png`

## Do Not Do

- Do not add business logic to Razor markup.
- Do not use ad hoc raw div layouts when existing components fit.
- Do not show process staffing/matching in workflow start.

## Acceptance Checklist

- Context menu and inspector actions are present in the right states.
- Add dialog shows selected workflow and input preview.
- Start dialog clearly confirms workflow run.
- Selection panel shows state and step count.
- Dialogs/menus/floating windows are readable and unclipped in screenshots.

## Proof Required

- Component tests for action catalog and page/dialog state.
- Playwright screenshot proof:
  - `.codex/bundles/project-structure-workflow-runs/proof/browser/project-structure-add-workflow-desktop.png`
  - `.codex/bundles/project-structure-workflow-runs/proof/browser/project-structure-start-workflow-confirmation.png`
  - `.codex/bundles/project-structure-workflow-runs/proof/browser/project-structure-workflow-summary-mobile.png`

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`
- Viewports: `1600x950`, `390x844`
- Actions/assertions: open project-structure canvas, right-click eligible node, verify Add workflow, open add dialog, select workflow, verify input preview includes project/parent, create workflow node, right-click workflow node, verify Start, open confirmation, select workflow node and verify status detail.
- Review questions: no clipping, no lateral overflow, dialogs above canvas chrome, readable input preview, no process matching/staffing stage.

## Progression Gate

- Component tests pass and browser screenshots prove the add/start/status UI is usable.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
