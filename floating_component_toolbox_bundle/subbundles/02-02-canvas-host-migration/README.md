# 02 Canvas Host Migration

## Status

- Status: `Ready`

## Objective

- Migrate existing project structure, process canvas, and prompt factory floating toolboxes to the shared toolbox body while preserving all existing domain action flows.

## Covered Inputs

- R1: different canvases have different components but should use a generic toolbox principle.
- R1: project and process structure canvases must not break.
- R2: implement the generic way on all places where it must be.

## Prerequisites

- Subbundle 01 shared toolbox contract completed and gate passed.
- Existing project/process/prompt add callbacks identified and preserved.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolboxWindow.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolboxWindow.razor.css
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ToolWindows.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasToolboxWindow.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceStepsTab.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.ToolWindows.cs

## Deliverables

- Project structure toolbox uses generic toolbox rendering and keeps `HandleToolboxActionSelectedAsync`.
- Process canvas toolbox uses generic toolbox rendering and keeps `OpenToolboxActionAsync`.
- Prompt factory toolbox uses generic toolbox rendering and keeps `AddComponentFromToolboxAsync` and preview behavior.
- Host-specific CSS reduced to layout adapters where needed.

## Dependency Impact

- Web app regression proof depends on this migration.
- Prompt factory and project/process canvas Playwright validations depend on the migrated markup keeping stable test IDs or providing equivalent selectors.

## Validation Depth

- Build affected modules or the web app project.
- Use existing targeted tests where available for project structure toolbox and prompt factory toolbox.
- Browser smoke project structure, process canvas, and prompt factory toolbox open states before final validation.

## Implementation Steps

- Convert project structure groups to OverlayLib toolbox sections/groups/items.
- Convert process groups to OverlayLib toolbox groups/items.
- Convert prompt factory section/group/block view models to OverlayLib toolbox sections/groups/items.
- Keep host-specific secondary preview action for prompt factory.
- Preserve data-testid values used by existing Playwright tests or add compatibility aliases.

## Do Not Do

- Do not change project structure create action IDs.
- Do not change process role/step template action IDs.
- Do not change prompt factory component block keys or tokenized create-dialog flow.
- Do not remove CanvasFloatingWindow wrappers from CanvasLib hosts.

## Acceptance Checklist

- Project structure toolbox opens, searches, groups, and triggers the same action IDs.
- Process canvas toolbox opens, searches, and opens the same role/step editor flow.
- Prompt factory toolbox opens, searches, previews, and adds components through the same logic.
- Minimize/hide/restore still works because the floating window shell is unchanged.

## Proof Required

- Build output for affected modules.
- Targeted test output if existing tests cover selectors.
- Playwright MCP screenshots for open toolbox states.
- Playwright MCP proof that project structure block add creates a visible canvas node.

## Browser Validation Logging

- Log route, viewport, actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.

## Progression Gate

- WebGL and final validation can continue only if canvas host migrations preserve the original add flows.

## Suggested Agent Prompt

- Migrate the three existing canvas-hosted floating toolboxes to the shared OverlayLib toolbox component through adapters. Keep all action callbacks and test IDs stable, then validate open states and add flows.
