# toolbar-triggered selected-subtree recomposition workflow

## Status

- `Completed`

## Objective

- Add the manual project structure toolbar button and selection-scoped workflow that invokes subtree recomposition, reloads the canvas, and gives clear feedback without introducing hidden automatic layout behavior.

## Covered Inputs

- `N001` new toolbar button for recomposition
- `N002` command must be manual, not automatic
- `N003` command scope comes from the selected node
- `N005` layout should use the space around the selected root more efficiently
- `N006` no collisions can remain visible on the canvas
- `N009` the screenshot complaint about one-direction growth and unused root space
- `N012` readability and branch distance matter more than over-packing
- `N014` browser proof must use the large `project-structure-mcp-validation-1 workbench` project

## Prerequisites

- Subbundle `01-subtree-radial-layout-engine-and-persistence-foundation` is completed
- The recomposition seam is stable enough to call from the page

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeEditing.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Components\ProjectStructureCanvas.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs

## Deliverables

- A new toolbar button that invokes selected-subtree recomposition
- Page-level workflow logic that calls the service, reloads the surface, and shows success or warning feedback
- Disabled-state or guard behavior when the current selection cannot drive meaningful recomposition
- Component and browser proof that the recomposed canvas uses space better and stays collision-free
- Browser proof that the selected validation project no longer clusters branches on just the left side of the root

## Dependency Impact

- Subbundle `03` depends on this workflow because closure proof must exercise the real user entry point, not the service in isolation.
- Weak proof here would leave the user-visible change unresolved even if the engine exists.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add a new toolbar button near the existing project structure toolbar toggles.
2. Wire the button to the selected-node workflow without introducing any automatic triggers.
3. Reuse existing feedback patterns such as `workflowFeedback` so the result is explicit when the command succeeds or cannot do useful work.
4. Reload the surface after recomposition and keep the selected node active.
5. Add component coverage for the new button, command invocation, and feedback behavior.
6. Add Playwright coverage or a focused browser flow that proves the real page improves space usage and keeps nodes collision-free.

## Scope Exceptions

- This phase does not redesign the broader canvas toolbar system.
- This phase does not add a secondary selection-window entry point unless implementation reality proves the toolbar alone is insufficient.

## Do Not Do

- Do not make recomposition run during ordinary selection changes.
- Do not hide failure or no-op cases behind silent returns.
- Do not reconnect or reparent nodes from the page.
- Do not move unrelated nodes outside the selected subtree to “help” the algorithm.

## Acceptance Checklist

- The toolbar renders a recomposition button on the project structure page.
- Clicking the button recomposes the selected subtree and reloads the canvas.
- The action remains manual and selection-scoped.
- The user receives explicit feedback after the command runs.
- Browser-visible layout after recomposition uses the space around the selected node more effectively than before.
- No visible node collisions remain after the command finishes.
- First-layer groups read as separate branch wedges or bubbles instead of interleaving too tightly.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProjectStructurePageRecompositionTests" --nologo`
- Real browser run on `/projects/<projectId>/structure`, specifically the large `project-structure-mcp-validation-1 workbench` project
- Large-screen screenshot that shows the subtree before or after recomposition using space around the selected root
- DOM or canvas evaluation that confirms node rectangles do not overlap after recomposition
- Narrower-width follow-up screenshot because the change affects layout density

## Browser Validation Logging

- Route: `/projects/<projectId>/structure` for `project-structure-mcp-validation-1 workbench`
- Viewports:
  - `1600x1000` large-screen first pass
  - `1280x820` narrower follow-up
- Playwright actions:
  - open the structure page
  - select the intended subtree root
  - click the new toolbar button
  - evaluate node bounds for overlap
  - capture screenshots
- Screenshot paths:
  - `output/project-structure-node-recomposition-bundle-1/recompose-desktop.png`
  - `output/project-structure-node-recomposition-bundle-1/recompose-narrow.png`
- Screenshot review questions:
  - is the unused space around the selected root reduced?
  - are any nodes overlapping or clipped?
  - does the subtree remain readable without extra panning?
  - do first-layer branches read as separated clockwise groups instead of one left-heavy stack?

## Progression Gate

- The toolbar command must pass targeted component proof and a real browser pass that shows the selected subtree recomposes without overlap and without hidden automation.

## Suggested Agent Prompt

```text
Implement subbundle 02 only.
Add the project structure toolbar command and page workflow that invokes the already-shipped recomposition seam.
Keep the behavior manual, selection-scoped, and explicit, then prove it through component and browser validation.
```
