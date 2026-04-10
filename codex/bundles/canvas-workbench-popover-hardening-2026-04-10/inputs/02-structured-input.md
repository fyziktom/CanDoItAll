# Structured Input

## Core Objective

- Repair the shared canvas-workbench annotation popover path so workbench canvases stop throwing inside `syncSceneHoverState`, remain stable around node clicks and refreshes, and close this report with explicit browser proof instead of speculative reasoning.

## Hard Constraints

- Preserve all current functionality, especially annotation actions and workbench interaction flows.
- Keep the change set minimal and inside the shared JS runtime unless the repo proves a consumer-specific issue.
- Treat this as shared `CanvasWorkbench` logic, not a one-page workaround.

## Source Artifacts

- The user report and stack trace in `inputs/00-original-request.md`
- The shared runtime files under `CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench`
- The shared canvas sandbox route in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Canvas.razor`
- The real workbench consumer route in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`

## Input Coverage Signals

- The raw note explicitly names `showPopover`, workbench canvases, repeated failures, node-click correlation, robustness across all nodes and situations, nearby JS anti-patterns, and preservation of functionality.
- None of those signals can be collapsed into “fix one crash” without losing scope.

## Dependency And Sequencing Signals

- The split-file crash path and hover-state invariants must land before any broader “all nodes and situations” hardening proof can be trusted.
- Browser closure depends on having both the shared canvas route and, when available, a real workbench route to smoke the same runtime.

## Validation Expectations

- Run prepared-stage bundle validation before implementation.
- Run targeted .NET validation after the JS changes land.
- Use real browser proof for annotation hover and click behavior.
- Update raw-note closure and gate rows from actual results.

## UI Validation Strategy

- Start with a large-screen pass on `/groups/canvas`.
- Open the popover state itself and review readability, clipping, lateral overflow, and z-order.
- Follow with a narrower-width pass if popover placement or overlap behavior changes.
- Use a workbench route smoke when environment data allows it.

## Browser Validation Analytics

- Subbundle 01 logs the shared-canvas hover fix with route, viewport, actions, and one open-popover screenshot.
- Subbundle 02 logs repeated hover and click sequences across multiple annotation-bearing nodes and routes.
- Subbundle 03 logs final regression results, screenshots, and raw-note closure evidence.

## Working Assumptions

- The reported stack is coming from the shared runtime loaded from `CanDoItAll.Components.CanvasLib`.
- Annotation hover is the specific trigger path because `syncSceneHoverState` only routes to popover logic for annotation hits.
- The sandbox route shares enough runtime behavior to serve as the first browser truth surface.

## Primary Risks

- Fixing only the direct `showPopover` call while leaving stale hover state intact would leave intermittent failures behind.
- Consumer-specific proof could diverge from sandbox proof if the workbench route exercises additional rerender flows.
- Browser caches can hide the true JS result unless the runtime is rebuilt and reloaded cleanly.
