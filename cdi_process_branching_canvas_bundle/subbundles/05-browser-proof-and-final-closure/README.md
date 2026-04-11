# Browser Proof And Final Closure

## Status

- `Ready`

## Objective

- Close the reopened request with real browser proof, screenshot review, raw-note closure for the new follow-up notes, validator passes, and final bundle synchronization.

## Covered Inputs

- `N007` Screenshot-style multi-port visual target.
- `N008` Real Playwright validation with screenshots.
- `N011` Left click starts connector authoring and left click confirms it on a target circle.
- `N012` Connector circles must sit exactly on their badges and none may be missing.
- `N013` Many-to-many routing semantics must be supported or blocked honestly.
- `N014` Moved derived nodes must persist and not snap back after later interactions.
- `N015` Repair the bundle before implementing the latest follow-up scope.

## Prerequisites

- `subbundles/01-scenario-definition-and-live-gap-reconciliation` must be `Completed` and trusted.
- `subbundles/02-advanced-canvas-node-contract` must be `Completed` and trusted.
- `subbundles/03-process-branch-node-authoring-and-mapping` must be `Completed` and trusted.
- `subbundles/04-software-development-branching-examples-and-regression-coverage` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor`
- `C:\repositories\CanDoItAll\cdi_process_branching_canvas_bundle\README.md`
- `C:\repositories\CanDoItAll\cdi_process_branching_canvas_bundle\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\cdi_process_branching_canvas_bundle\analysis\03-architecture-troubles-log.md`
- `C:\repositories\CanDoItAll\cdi_process_branching_canvas_bundle\traceability\01-requirement-traceability.md`

## Deliverables

- Final large-screen and narrower-width browser proofs recorded in the execution report.
- Raw-note closure table updated for the initial request and the latest follow-up notes.
- Final bundle synchronization and validator passes.

## Dependency Impact

- This is the closure phase. If proof is weak here, the workflow is not complete.
- The reopened scope must close with evidence for gesture, geometry, many-to-many truth, and persistence, not only a prettier screenshot.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Reopen the initial request and both follow-up messages.
2. Run the final browser walkthrough on `/processes`.
3. Capture and review screenshots at large and narrower widths, including close-ups of the relevant connector circles.
4. Verify movement persistence with a rerender-triggering interaction and a reread or refresh.
5. Update the execution report, raw-note closure table, and validation summary.
6. Run the completed-stage bundle validator and close only if it passes.

## Do Not Do

- Do not treat passing tests alone as final closure.
- Do not mark a note solved without citing browser or code proof when the note is UI-visible.
- Do not call many-to-many solved unless both the stored data and the reloaded surface preserve the intended joins.

## Acceptance Checklist

- Final browser screenshots are captured and reviewed.
- Left-click source and target circle authoring is proven in the browser.
- Badge-circle alignment and the router-side decision-role circle are visibly correct.
- Movement persistence is proven after a later interaction and a reread or refresh.
- The raw-note closure table is complete.
- The final bundle validator passes.

## Proof Required

- Playwright walkthrough on `/processes` with screenshots at large and narrower widths.
- Final test and validation commands recorded in `reviews/01-execution-report.md`.
- Completed-stage bundle validator pass recorded in the execution report.

## Browser Validation Logging

- Route: `/processes`
- Viewports: `Large-screen desktop` and `1280x800`
- Playwright MCP actions: navigate, perform left-click connector authoring, inspect close-up badge alignment, move nodes, trigger a later interaction, capture screenshots
- Expected evidence path: final screenshots recorded in `reviews/01-execution-report.md`

## Progression Gate

- The workflow ends only after every raw note has a closure status with proof and the completed-stage validator passes.

## Suggested Agent Prompt

```text
Implement this subbundle only. Reopen the initial request and both follow-up notes, run the final browser proof on /processes, prove left-click connector authoring, badge alignment, and movement persistence, update the raw-note closure and execution report, and pass the final bundle validator before closing the workflow.
```
