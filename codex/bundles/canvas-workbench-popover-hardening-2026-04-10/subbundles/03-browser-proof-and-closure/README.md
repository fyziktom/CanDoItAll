# Browser proof and closure

## Status

- `Completed`

## Closure Note

- Final evidence includes a successful managed build on `src\CanDoItAll.Web\CanDoItAll.Web.csproj`, workbench-route Playwright proof, and explicit documentation that the sandbox canvas sample currently has no annotation-bearing nodes for this bug class.

## Objective

- Close the report with actual validation, browser analytics, screenshot review, and raw-note closure so the runtime repair is proven rather than merely plausible.

## Covered Inputs

- `N001` through `N005`
- `R005` preserve existing behavior
- `R007` complete targeted validation and real browser proof

## Prerequisites

- `01-hover-and-popover-state-invariants` completed
- `02-canvas-runtime-hardening-across-node-interactions` completed

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Canvas.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\SharedCanvasBrowserTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`
- `C:\repositories\CanDoItAll\codex\bundles\canvas-workbench-popover-hardening-2026-04-10\reviews\01-execution-report.md`

## Deliverables

- Targeted validation results recorded in the execution report
- Browser-validation analytics rows populated with real evidence
- Raw-note closure rows updated to solved, partially solved, or not solved with proof
- Completed-stage bundle validation status

## Dependency Impact

- This is the closure phase. If proof is weak here, the bundle reopens earlier subbundles instead of shipping uncertainty as a summary paragraph.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run the targeted validation commands for the affected projects or tests.
2. Use Playwright on the shared canvas route and the workbench route when available.
3. Capture and review screenshots with the popover open.
4. Update the execution report, raw-note closure table, and final bundle status.
5. Run completed-stage bundle validation.

## Scope Exceptions

- If the workbench route cannot be reached because no seeded project is available, record that exact blocker and keep the gap visible in the execution report.

## Do Not Do

- Do not claim closure from code inspection alone.
- Do not replace open-state popover proof with closed-trigger screenshots.
- Do not hide missing workbench-route proof behind a generic residual-risk sentence.

## Acceptance Checklist

- Targeted validation completed and recorded.
- Browser analytics rows are populated with real route, viewport, actions, screenshots, and result values.
- Screenshot review explicitly answers readability, clipping, overflow, and layering questions.
- Every raw note has a non-pending closure status backed by proof.
- Completed-stage bundle validation passes.

## Proof Required

- Targeted build or test commands that exercise the affected workbench surface or shared runtime consumers.
- Playwright proof on `/groups/canvas`.
- Playwright or equivalent smoke proof on `/projects/{ProjectId}/structure` when available.
- Open-popover screenshots reviewed on a large viewport and a follow-up narrower viewport if needed.

## Browser Validation Logging

- Primary closure route: `/groups/canvas`
- Secondary closure route: `/projects/{ProjectId}/structure` when available
- Required viewport passes: `1600x900` and `1280x800`
- Required Playwright actions: navigate, locate annotation-bearing node, hover annotation, click related target, re-hover, inspect console, and capture screenshots with the popover open
- Expected screenshots: one desktop screenshot from shared canvas, one desktop screenshot from workbench route when available
- Required visual review: readable text, no clipping, no overflow that hides critical content, correct z-order above workbench chrome and floating windows

## Progression Gate

- Bundle closure is allowed only when all raw notes have explicit proof-backed statuses and the completed-stage validator passes.
- Closure decision: ready for completed-stage validation.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Run the real validation and browser proof for the shared CanvasWorkbench popover repair, then update every bundle closure artifact from the actual evidence.
```
