# 08 Workflow Canvas Floating Windows, Modals, and Tabs

## Status

- `Completed`

## Objective

Move workflow authoring controls into canvas floating windows, use modals for node creation and double-click node details/editing, and split the workflows page into clear operational tabs without weakening existing run/catalog behavior.

## Covered Inputs

- `inputs/03-follow-up-request.md`: workflow canvas toolbox and selection must be floating windows; adding new items must show a modal; double-click node must open modal with details/edit; workflows page should use tabs.

## Prerequisites

- Subbundles `01` through `05` remain valid enough to supply executor descriptors, quick-create action ids, canvas mapping, and setup editing fields.
- The project-structure canvas floating window pattern remains available.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Core\CanvasFloatingWindow.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\Dialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolboxWindow.razor`

## Deliverables

- Workflow toolbox floating window inside `CanvasWorkbench.OverlayContent`.
- Workflow selection/node-list floating window inside the canvas.
- Optional component-library floating window when it improves the same authoring flow.
- Toolbar buttons to show/hide the workflow floating windows.
- Modal workflow creation path for toolbox and right-click create actions.
- Double-click node modal using `NodeOpened` that includes node summary and the existing typed editor fields.
- Workflows page tabs for dashboard, definitions, editor, templates, history, and analytics using BaseLib tabs.

## Dependency Impact

- This is a critical UI foundation for later browser and scenario proof.
- Scenario seeding should not start until the modal add/edit and floating-window flows can be validated in a browser.

## Validation Depth

- Compile validation.
- Focused component/unit validation where practical.
- Playwright browser proof for large and narrower viewports.

## Implementation Steps

1. Wire `CanvasWorkbench @ref`, `ToolbarLeftContent`, `OverlayContent`, `NodeOpened`, and state persistence in `WorkflowCanvasEditor`.
2. Extract repeated node settings markup only as needed to avoid duplicating the modal and selection-window editor.
3. Change workflow create actions/toolbox selections to open a create modal/composer before adding a node.
4. Add workflow page tabs and move existing panels into the right tab groups.
5. Keep existing test ids or add stable new ones for floating windows and dialogs.

## Scope Exceptions

- Do not implement a plugin-rendered setup component system in this subbundle.
- Do not implement full workflow persistence in this UI subbundle.

## Do Not Do

- Do not introduce raw canvas-only HTML controls where BaseLib/CanvasLib components already exist.
- Do not remove the existing run/test/validation capabilities while rearranging tabs.

## Acceptance Checklist

- Toolbox and selection are floating windows inside the workflow canvas.
- Adding via toolbox and right-click opens a modal or composer before the node lands.
- Double-clicking a node opens a details/edit modal.
- Workflows page uses tabs and preserves dashboard/catalog/editor/history/analytics surfaces.
- Existing executor setup fields remain available.

## Proof Required

- `dotnet build CanDoItAll.slnx --no-restore`
- Browser proof at `/agents/workflows` showing tabs, floating toolbox, floating selection, add modal, and node details modal.
- Screenshots saved under `artifacts/browser/`.

## Browser Validation Logging

- Record route, viewport, actions, assertions, screenshot paths, and pass/fail result in `reviews/01-execution-report.md`.

## Progression Gate

- Pass only when the browser shows open floating windows and modals without clipping, overlap, or lost editor controls.

## Suggested Agent Prompt

Implement subbundle 08 only. Reuse CanvasLib floating windows and BaseLib Dialog/Tabs. Keep changes scoped to workflow page/canvas UI and record browser proof before closing.
