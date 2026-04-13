# Shared CanvasLib recomposition engine and menu contract

## Status

- `Completed`

## Objective

- Add the shared C# recomposition and toolbar-menu foundation that later process-specific work can reuse without embedding all layout math inside the processes page.

## Covered Inputs

- `N004` Three recomposition commands.
- `N005` One hover dropdown toolbar control.
- `N006` Share common parts across CanvasLib-backed surfaces.
- `N007` Run calculations in C#.

## Prerequisites

- `subbundles/01-workspace-density-and-viewport-width-foundation` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Graph\Composition\LayoutEngine.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasToolbarActions.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureSubtreeRecompositionEngine.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolbarActions.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`

## Deliverables

- A shared strongly-typed recomposition intent model.
- Shared C# geometry and movement planning for collision removal and spacing expansion.
- A toolbar-menu contract that can host the recomposition options under one compact control.
- Focused automated coverage for the shared math and menu behavior.

## Dependency Impact

- `subbundles/03` depends on this phase for both the shared command vocabulary and the reusable collision and spacing primitives.
- Weak proof here would force process-specific code to become a one-off implementation and would fail the modularity request.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Introduce or extend a shared recomposition-planning seam in CanvasLib using strong types.
2. Implement generic collision-removal and spacing-expansion planning in C#.
3. Add a reusable toolbar-menu presentation contract for a hover-revealed recomposition control that also works by click and focus.
4. Wire the processes toolbar to the shared menu contract without adding process-smart behavior yet.
5. Add focused automated tests for the shared planning behavior and any menu rendering logic.

## Scope Exceptions

- The smarter fishbone-style process recomposition is deferred to `subbundles/03`.

## Do Not Do

- Do not migrate the existing project-structure recomposition engine into the new shared seam unless a small extraction is clearly justified.
- Do not embed process-domain concepts such as roles or branch outcomes directly into shared CanvasLib types.
- Do not claim closure with browser proof alone; shared math needs automated coverage.

## Acceptance Checklist

- The shared code can express `Collisions`, `Add Space Around`, and `Recomposition` as typed commands.
- Collision-removal and spacing planning happen in C#.
- A single compact toolbar control can reveal the recomposition actions.
- The shared contract is process-agnostic enough to reuse elsewhere.

## Proof Required

- Focused automated tests for shared recomposition planning.
- A browser smoke on `/processes` showing the new toolbar control exists and exposes the three actions.
- Short architecture notes in `reviews/01-execution-report.md` confirming the shared and process boundary stayed clean.

## Browser Validation Logging

- Route: `/processes`
- Viewport: `1600x900`
- Required Playwright actions:
  - navigate to `/processes`
  - inspect the canvas toolbar
  - hover and click the recomposition control
  - confirm the three action labels render
  - capture screenshot
- Expected evidence path:
  - `C:\repositories\CanDoItAll\output\playwright\process-recomposition\01-toolbar-menu.png`
- Screenshot review questions:
  - Is the menu discoverable without adding heavy chrome?
  - Does the menu remain readable and compact?

## Progression Gate

- `subbundles/03-process-canvas-integration-and-managed-sqlite-application` may continue only after shared tests pass and the toolbar control exposes the required commands in the browser.

## Suggested Agent Prompt

```text
Implement this subbundle only. Add a shared C# recomposition-planning seam with collision and spacing support, create a reusable toolbar menu contract for the three recomposition actions, keep the boundary process-agnostic, add focused tests, and prove the control renders on /processes before closing the phase.
```
