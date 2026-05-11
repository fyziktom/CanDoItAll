# Workflow canvas toolbox and node setup UI

## Status

- `Completed`

## Objective

- Make workflow executors discoverable and configurable in the workflow canvas through grouped right-click actions and a component toolbox.

## Success Criteria

- Right-click/create menu exposes executors as a second-level grouped menu.
- Workflow component toolbox exposes grouped/searchable executor entries similar to the project-structure canvas pattern.
- Creating an executor node stores executor id, default settings JSON, default policy, and result shape.
- Inspector displays descriptor-backed setup fields for built-in executors and preserves setup renderer key for future plugins.

## Covered Inputs

- R13, R14, R15.

## Prerequisites

- Subbundle 01 descriptors exist.
- Existing workflow editor save/validation flow is understood.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.OverlayLib\Components\Core\OverlayComponentToolbox.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolboxWindow.razor`

## Deliverables

- Descriptor-backed executor actions in workflow canvas.
- Toolbox UI for executor categories.
- Built-in setup editing for storage, HTTP, spreadsheet, project-structure, and image executor settings.
- Browser evidence for right-click and toolbox paths.

## Dependency Impact

- Subbundle 06 needs UI-created executor nodes to run realistic workflow scenarios.
- Future plugin setup depends on stable renderer key preservation.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Inject/read executor catalog in the workflow canvas surface.
2. Map descriptors to grouped `CanvasWorkbenchAction.Children`.
3. Add component toolbox window or pane using existing `OverlayComponentToolbox` pattern.
4. Add create-node path that applies descriptor defaults.
5. Add inspector fields for built-in setup models with typed enums/known options.
6. Add component/unit tests where existing test infrastructure supports it.
7. Run browser proof on desktop and narrower viewport.

## Scope Exceptions

- Remote plugin-rendered Razor setup components are deferred.
- Full schema-driven dynamic form engine is not required for this first pass.

## Do Not Do

- Do not force users to edit raw JSON for common built-in executors.
- Do not introduce a new UI library.
- Do not bypass existing component wrappers/patterns with ad hoc floating cards.

## Acceptance Checklist

- Right-click second-level executor menu is visible and creates the selected executor node.
- Toolbox shows at least storage, project, HTTP, image, and spreadsheet groups.
- Inspector can edit the minimum useful settings for built-in executors.
- Saved workflow definition contains executor id/settings/policy.

## Proof Required

- Build/test command covering UI compilation.
- Browser route, viewport, DOM/action notes, and screenshots recorded in execution report.
- Screenshot review confirms menus/toolbox do not overlap or truncate critical text.

## Browser Validation Logging

- Target route: workflow builder/editor route.
- Required viewports: `1600x900` and a narrower viewport around `390x844` if the route supports responsive layout.
- Required actions: navigate, open canvas right-click menu, open executor submenu, create executor node, open toolbox, create a second executor node, inspect settings.
- Evidence paths: `artifacts/browser/workflow-executors-desktop.png` and `artifacts/browser/workflow-executors-narrow.png`.
- Review questions: Are executor groups discoverable? Does text fit? Are setup fields usable without raw JSON? Are menus/toolbox visually consistent with existing canvases?

## Progression Gate

- Subbundle 06 may run UI-authored scenarios only after browser proof shows both authoring paths working.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
