# Cross-canvas app proof and blockers

## Status

- `Completed`

## Objective

- Prove the repaired CanvasLib behavior across the real CanDoItAll app routes that currently host workbench or calendar canvases, and explicitly record any non-canvas blockers that prevent route-level validation.

## Covered Inputs

- `N010` test all canvases used in the CanDoItAll app and repair what is still wrong or buggy
- `R014` route proof must cover reachable app canvases and log blocked routes honestly when the blocker is outside the canvas fix

## Prerequisites

- `07-workbench-interop-lifecycle-hardening`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectCalendarPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`

## Deliverables

- Browser proof for reachable workbench and calendar routes in the app
- A recorded blocker note for any route that cannot reach its canvas due to an unrelated failure

## Dependency Impact

- This is the route-level closure phase for the reopened lifecycle work. If the route inventory or proof is wrong, the bundle could be closed while leaving a real app canvas surface unverified.

## Validation Depth

- `Real app route matrix`

## Implementation Steps

1. Reconfirm the actual app routes that host CanvasLib surfaces and separate workbench, calendar, and blocked non-canvas routes.
2. Prove the repaired workbench lifecycle on `ProjectStructurePage` and `ProcessWorkspace` without reintroducing console errors.
3. Prove the calendar route still loads with a live calendar host after the shared CanvasLib changes.
4. Inspect `PromptFactoryPage` and record any non-canvas blocker honestly instead of widening this bundle into an unrelated failure.

## Scope Exceptions

- Do not fix unrelated Prompt Factory content-generation or manifest issues inside this canvas bundle unless the investigation shows the canvas refactor caused them.

## Do Not Do

- Do not mark a route as proven if it failed before its canvas could initialize.
- Do not widen this phase into speculative refactors once the route matrix is verified.
- Do not hide a non-canvas blocker behind vague wording; log the exact failure.

## Acceptance Checklist

- `ProjectStructurePage` loads with a connected workbench host and no browser console errors
- `ProcessWorkspace` `Steps` and `Runs` canvases load with connected workbench state and no browser console errors
- `ProjectCalendarPage` loads with a live calendar host and no browser console errors
- `PromptFactoryPage` is either proven or explicitly logged as blocked by a non-canvas failure

## Proof Required

- Browser interaction and console checks on the reachable routes
- At least one screenshot on the repaired Processes Run canvas
- Final bundle validator pass after the reopened evidence is recorded

## Browser Validation Logging

- Routes under test: `/projects/{ProjectId}/structure`, `/projects/{ProjectId}/processes`, `/projects/{ProjectId}/calendar`, and `/prompt-factory`
- Required viewport passes: `1600x900`
- Required Playwright actions: load each reachable route, confirm the expected canvas host is present, inspect runtime state or host connectivity, and review the console for errors
- Expected screenshots: at least one screenshot on the repaired Processes `Runs` canvas; screenshots on the other routes are optional unless a visual regression appears
- Required visual review: structure and processes workbench canvases render normally, calendar host is live, and blocked routes are documented with the exact non-canvas failure

## Progression Gate

- The bundle may close only after all reachable app canvas routes are re-proved and any blocked route is tied to an explicit non-canvas failure.

## Closure Note

- Completed on `2026-04-10`.
- `ProjectStructurePage`, `ProcessWorkspace` `Steps` and `Runs`, and `ProjectCalendarPage` were re-proved successfully with clean console captures.
- `PromptFactoryPage` was inspected and logged as blocked by a missing `output/prompt-library/manifest.json`, which is outside the canvas lifecycle fix.

## Suggested Agent Prompt

```text
Implement subbundle 08 only. Re-prove every reachable CanDoItAll app canvas route after the lifecycle fix, and record any blocked route with the exact non-canvas failure instead of widening the bundle.
```
