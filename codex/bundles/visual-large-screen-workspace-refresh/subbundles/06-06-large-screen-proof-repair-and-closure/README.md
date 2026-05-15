# 06-large-screen-proof-repair-and-closure

## Status

- `Completed`

## Objective

- Prove the visual refresh with large-screen browser evidence, compare screenshots against the Economy reference, repair visible issues, and close every raw note honestly.

## Covered Inputs

- RN-001 improve visual look, working space, and clarity.
- RN-002 large-screen-only hard rule.
- RN-006 database flyout open-state proof.
- RN-008 screenshot repair loop.
- RN-009 no own CSS.
- RN-010 dialogs for too much information.
- RN-012 professional B2B presentation readiness.

## Prerequisites

- SB00-01, SB00-02, SB00-03, SB01, SB02, SB03, SB03-04, SB04, SB04-05, SB05, and SB05-06 closure gates passed or blockers are explicitly recorded.
- All changed routes have screenshot paths in `reviews/01-execution-report.md`.
- Test commands from prior subbundles have either passed or are recorded as blockers.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\traceability\01-requirement-traceability.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\reference-02-run-observation-page.png`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\reference-11-run-bus-tab.png`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\analysis\03-imagegen-proposal-review.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PlaywrightAppFixture.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\DatabaseSwitchWorkbenchPlaywrightTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureProcesses.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs`

## Deliverables

- Final large-screen screenshot set for representative and changed routes.
- Overlay/open-state proof for collapsed nav tooltips, expanded nav, DB flyout, dialogs, and tree surfaces.
- Screenshot repair log with before/after decisions.
- Populated execution report gate rows, browser analytics, analytics review, and raw-note closure.
- Final validator pass or explicit blockers.

## Dependency Impact

- This is the closure gate for the whole bundle. Weak proof here means the bundle is not complete, even if code changes build.

## Validation Depth

- End-to-end UI proof, visual repair, and closure gate.

## Implementation Steps

1. Reopen original raw request and reference screenshots.
2. Reopen final screenshots from each prior subbundle.
3. Compare shell, tree surfaces, and page density against the Economy reference direction.
4. Record visible issues: wasted width, crowded cards, unreadable labels, clipped overlays, excessive text, broken tree hierarchy, or missing DB controls.
5. Repair issues in the owning subbundle scope, or reopen the owning subbundle if the repair is not local.
6. Run targeted tests and a final build/test confirmation suitable for the touched files.
7. Populate `reviews/01-execution-report.md` with final gate, browser analytics, analytics review, and raw-note closure.
8. Run `validate_bundle.py --stage completed` only when execution is actually complete; for preparation-only handoff, run prepared-stage validation.

## Scope Exceptions

- Small and medium screen polish remains out of scope.
- Any unsolved route must become an explicit blocker or follow-up subbundle, not residual-risk prose.

## Do Not Do

- Do not declare completion with missing screenshot rows.
- Do not count generated `imagegen` assets as proof.
- Do not ignore overlay clipping or text overlap.
- Do not hide unresolved raw notes in summary prose.

## Acceptance Checklist

- Collapsed shell, expanded shell, nav tooltip, DB flyout, and bottom Settings/DB actions have final screenshots.
- Projects/processes/workflows tree surfaces have final screenshots and interaction proof.
- Every changed route has a final large-screen screenshot.
- Every raw note is marked Solved, Partially solved, or Not solved with proof.
- No new page-local CSS violation remains.
- Final build/test and validator results are recorded.

## Proof Required

- Large-screen Playwright screenshot set.
- Overlay open-state screenshot set.
- Targeted `dotnet test` results for changed areas.
- Final `scripts/validate_bundle.py --stage completed` result if implementation is complete.
- Updated execution report with no pending final closure rows.

## Browser Validation Logging

- Routes: final changed route set plus `/`, `/projects`, `/processes`, `/agents/workflows`, `/settings`, and any route repaired in this subbundle.
- Viewport: large desktop, recommended `1920x1080`.
- Actions: route navigation, collapsed nav hover/focus, expand nav, DB flyout hover/focus/copy, tree expand/select, dialogs open/close, main page scroll where applicable.
- Screenshots: final route screenshot set, overlay screenshot set, before/after repair pairs where repairs were made.
- Review questions: does this look professional enough for a customer video, does it visibly use more workspace, does it resemble the reference density, are tree/list surfaces clearer, and are all details reachable.

## Progression Gate

- The bundle can close only when execution report rows are populated, raw-note closure is honest, validators pass, and unresolved gaps are represented as blockers or follow-up subbundles.

## Suggested Agent Prompt

```text
Implement subbundle 06 only. Run the large-screen visual proof and repair loop, compare final app screenshots to the reference screenshots, fix local visual issues or reopen owning subbundles, run targeted tests, populate all execution report rows, close raw notes honestly, and run the final validators only when implementation is actually complete.
```
