# Structured Input

## Core Objective

- Repair the shared canvas-workbench annotation popover path so workbench canvases stop throwing inside `syncSceneHoverState`, remain stable around node clicks and refreshes, and close this report with explicit browser proof instead of speculative reasoning.
- Extend the same verified runtime area with a maintainability pass that analyzes CanvasLib JS hotspots, splits the biggest workbench files into smaller feature slices, introduces shared helpers only where they remove real duplication or fragile cross-file coupling, and preserves every current behavior.
- Repair the newly reported real-app Processes Run-tab failure where `CanDoItAll.canvasWorkbench.selectNodes` receives a null host, then prove the reachable CanDoItAll app canvas routes rather than stopping at the original workbench page.

## Hard Constraints

- Preserve all current functionality, especially annotation actions and workbench interaction flows.
- Keep the change set minimal and inside the shared JS runtime unless the repo proves a consumer-specific issue.
- Treat this as shared `CanvasWorkbench` logic, not a one-page workaround.

## Source Artifacts

- The user report and stack trace in `inputs/00-original-request.md`
- The shared runtime files under `CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench`
- The shared canvas sandbox route in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Canvas.razor`
- The real workbench consumer route in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- The Processes workspace consumer in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- The project calendar consumer in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectCalendarPage.razor`
- The prompt factory consumer in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`

## Input Coverage Signals

- The raw note explicitly names `showPopover`, workbench canvases, repeated failures, node-click correlation, robustness across all nodes and situations, nearby JS anti-patterns, and preservation of functionality.
- None of those signals can be collapsed into “fix one crash” without losing scope.
- The follow-up explicitly asks for a search across CanvasLib JS files, identification of files that are too long, bundle expansion with new subbundles, execution of those subbundles, and working verification after the refactor.
- The reopened note explicitly names the Processes Run tab, a null-host `selectNodes` failure, and asks for testing across all canvases used in the CanDoItAll app.

## Dependency And Sequencing Signals

- The split-file crash path and hover-state invariants must land before any broader “all nodes and situations” hardening proof can be trusted.
- Browser closure depends on having both the shared canvas route and, when available, a real workbench route to smoke the same runtime.
- The new lifecycle fix must land before Processes tab-switch proof is trusted, because the real app failure occurs during after-render JS synchronization.

## Validation Expectations

- Run prepared-stage bundle validation before implementation.
- Run targeted .NET validation after the JS changes land.
- Use real browser proof for annotation hover and click behavior.
- Update raw-note closure and gate rows from actual results.
- Re-run the bundle validator after the organization subbundles land.
- Prove that the workbench route still loads, hovers, clicks, and exports the same runtime API after the file splits.
- Prove the real app `ProjectStructure`, `ProcessWorkspace`, and `ProjectCalendar` canvas surfaces after the lifecycle fix, and log blocked app routes explicitly when they fail before reaching CanvasLib.

## UI Validation Strategy

- Start with a large-screen pass on `/groups/canvas`.
- Open the popover state itself and review readability, clipping, lateral overflow, and z-order.
- Follow with a narrower-width pass if popover placement or overlap behavior changes.
- Use a workbench route smoke when environment data allows it.

## Browser Validation Analytics

- Subbundle 01 logs the shared-canvas hover fix with route, viewport, actions, and one open-popover screenshot.
- Subbundle 02 logs repeated hover and click sequences across multiple annotation-bearing nodes and routes.
- Subbundle 03 logs final regression results, screenshots, and raw-note closure evidence.
- Subbundle 05 logs the first workbench smoke after the `06` split.
- Subbundle 06 logs the final workbench regression pass after the `07` split.
- Subbundle 07 logs the Processes Run-tab lifecycle fix with real tab-switch proof.
- Subbundle 08 logs the reachable app-route matrix and any honest blockers.

## Working Assumptions

- The reported stack is coming from the shared runtime loaded from `CanDoItAll.Components.CanvasLib`.
- Annotation hover is the specific trigger path because `syncSceneHoverState` only routes to popover logic for annotation hits.
- The sandbox route shares enough runtime behavior to serve as the first browser truth surface.

## Primary Risks

- Fixing only the direct `showPopover` call while leaving stale hover state intact would leave intermittent failures behind.
- Consumer-specific proof could diverge from sandbox proof if the workbench route exercises additional rerender flows.
- Browser caches can hide the true JS result unless the runtime is rebuilt and reloaded cleanly.
- Splitting the wrong file boundary could create new shared-module load-order failures that are harder to diagnose than the original crash.
- Widening from workbench-runtime seams into unrelated calendar files would weaken proof quality and bundle focus.
- Some app routes that host canvases can still be blocked by unrelated server-side failures, which must be recorded without misattributing them to CanvasLib.
