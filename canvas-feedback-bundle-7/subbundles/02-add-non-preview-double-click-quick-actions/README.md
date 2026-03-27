# 02-add-non-preview-double-click-quick-actions

## Status

- `Ready`

## Objective

- Change non-preview double-click from immediate execution into a centered quick-action modal that offers a deliberate `Edit` action plus the best secondary action for that node type.

## Covered Inputs

- `N003` double-clicking an item without preview should open a centered in-canvas modal with square buttons, `Edit` first, and a second most-probable action such as `Run PowerShell` or `Open Wizard in New Tab`.
- `R004`
- `R005`
- `R006`
- `R007`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeEditing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.RuntimeLaunch.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables

- Page-owned quick-action modal state and markup for non-preview double-click.
- Explicit action mapping derived from existing Workbench command logic.
- Centered modal styling with square action buttons.
- Focused proof for representative node types and unsupported or non-editable handling.

## Implementation Steps

1. Keep shared canvas double-click reporting generic and move the non-preview branching decision into `ProjectStructurePage`.
2. Resolve quick actions from existing edit and command logic so the modal stays aligned with the Workbench action catalog.
3. Render a centered in-canvas modal for non-preview nodes with:
   - `Edit` first when edit is supported
   - one best secondary action for the current node type
   - explicit handling when the node is non-editable or has no valid secondary action
4. Wire modal button execution back into the current page and service command path, including refresh and navigation side effects where they already exist today.
5. Add focused proof for at least:
   - a script-like node that offers `Run PowerShell`
   - a prompt-related node that offers `Open Wizard in New Tab`

## Scope Exceptions

- Do not change preview-node double-click behavior.
- Do not replace the existing inspector, context menu, or command catalog with the quick-action modal.

## Do Not Do

- Do not hard-code node-type behavior in JavaScript.
- Do not duplicate the full context menu inside the modal.
- Do not silently auto-run the old open action for nodes that now require the quick-action modal.

## Acceptance Checklist

- Double-clicking a non-preview node opens a centered modal instead of executing immediately.
- The modal uses square action buttons.
- Editable nodes show `Edit` first.
- Representative node types show the correct secondary action label.
- Unsupported or non-editable nodes are explicit rather than silently pretending to support edit.

## Proof Required

- Add focused automated tests for quick-action resolution where feasible.
- Run a maximized browser pass and save a screenshot of the quick-action modal.
- Browser proof must demonstrate the correct button labels for at least one script node and one prompt-related node.
- Record any explicit node-type limitation in the execution report instead of treating it as implicitly acceptable.

## Suggested Agent Prompt

```text
Implement feedback7 subbundle 02 only.

Keep the modal page-owned. Reuse existing Workbench edit and command logic so non-preview double-click opens a centered quick-action modal with `Edit` first and one best secondary action for the node type.
```
