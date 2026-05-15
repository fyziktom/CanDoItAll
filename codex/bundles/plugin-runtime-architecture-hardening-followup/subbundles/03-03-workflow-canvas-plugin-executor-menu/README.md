# SB03 Workflow Canvas Plugin Executor Menu

## Status

- `Completed`

## Objective

Change workflow canvas right-click executor creation so plugin executors are grouped behind a generic plugin icon and plugin-specific submenu instead of appearing directly under `Executors`.

## Success Criteria

- Plugin executors are not direct children of the second-layer `Executors` menu.
- The `Executors` menu has one generic plugin entry when plugin executors exist.
- Each plugin gets its own submenu using plugin display name/icon metadata.
- Exact plugin executors appear under their plugin submenu.
- Built-in executors remain discoverable and behavior-compatible.

## Covered Inputs

- PRH-007 Workflow Canvas Plugin Executor Menu
- PRH-008 Plugin Icon Contract, only where menu grouping needs icon metadata
- FIND-006

## Prerequisites

- SB01 progression gate passed.
- Plugin executor source metadata is reliable enough to distinguish built-in vs plugin executors.
- Read the `Workflow Canvas` rows in `inventories/plugin-runtime-architecture-hardening-checklist.xlsx`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js`

## Deliverables

- Updated quick-create action tree builder that separates built-in and plugin executors.
- Plugin grouping logic based on `WorkflowExecutorDescriptor.Source`.
- Tests for the generated action hierarchy.
- Browser proof that nested context menu behavior works on the real canvas.

## Dependency Impact

- SB04 can improve icons after this structure exists.
- SB06 depends on this menu structure proving Docker executors appear under Docker only after installation.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Inspect current `BuildQuickCreateActions` output and tests, if any.
2. Add helper logic to classify plugin executors using source kind and plugin id.
3. Preserve existing built-in executor grouping/order unless a small adjustment is required for clarity.
4. Add one generic `Plugins` action under `Executors` when plugin executors exist.
5. Add plugin-specific child actions under `Plugins`.
6. Add exact executor actions under each plugin.
7. Ensure create action ids still resolve through the existing `BuildCreateActionId`/parse path.
8. Add tests for no plugin executors, one plugin, and multiple plugins with multiple executors.
9. Use browser validation to open the canvas context menu and create a plugin executor node from the nested menu.
10. Update execution report.

## Scope Exceptions

- Do not add final brand icon assets here unless SB04 has already completed.
- Do not rewrite CanvasLib submenu mechanics unless evidence shows the existing recursive menu cannot satisfy the requirement.
- Do not change workflow executor execution semantics.

## Do Not Do

- Do not hard-code Office365/Gmail/Docker grouping in the canvas.
- Do not list plugin executors directly under `Executors`.
- Do not break existing built-in executor creation actions.

## Acceptance Checklist

- [x] Generated action tree includes `Executors` -> `Plugins` -> plugin -> executor.
- [x] Plugin executors are absent from direct `Executors` children.
- [x] Multiple executors under Office365 stay grouped under Office365.
- [x] Built-in executors remain available.
- [x] Browser proof shows nested menu/grouping open in the workflow editor toolbox.

## Proof Required

- Unit/component test for action hierarchy.
- Browser screenshot/video path showing nested submenu state.
- Browser proof that selecting a plugin executor creates the expected node.
- Execution report update.

## Browser Validation Logging

- Target route: workflow canvas/editor route used by the app.
- Required viewport passes: maximized desktop; narrower pass only if menu positioning/layout changes.
- Required actions: right-click canvas, open `Executors`, open generic plugin entry, open a plugin submenu, select an executor.
- Screenshot evidence: `artifacts/sb03-canvas-plugin-menu-layered.png`, `artifacts/sb03-canvas-plugin-node-created.png`.
- Review questions: Are plugin executors hidden from the second `Executors` layer? Can an Office365-style plugin hold multiple executor choices without crowding the second layer?

## Progression Gate

- SB04 and SB06 may rely on workflow menu proof only after the browser shows a plugin executor created through the nested plugin submenu.

## Suggested Agent Prompt

```text
Implement SB03 only from C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-architecture-hardening-followup.
Change the workflow canvas quick-create menu tree so plugin executors are grouped behind a generic plugin entry and plugin-specific submenu. Keep built-in behavior intact. Add hierarchy tests and browser proof, then update reviews/01-execution-report.md.
```
