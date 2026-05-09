# 04 - Browser Proof And Closure

## Status

- Status: `Completed`
- Closed: `Yes`
- Proof: `component test build`, `focused component tests`, `web build`, browser screenshots, `browser-proof.json`, completed-stage bundle validator`

## Objective

Prove the full assignment tuning in tests, build, real browser screenshots, proof JSON, raw-note closure, and final bundle validation.

## Covered Inputs

- IN-001
- IN-002
- IN-003
- IN-004
- IN-005

## Prerequisites

- Subbundles 01, 02, and 03 closure gates pass.
- The local web app can be launched or is already running.

## Exact Source References

- `C:\repositories\CanDoItAll\project-structure-process-assignment-tuning-bundle\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureProcessAssignmentDialogTests.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`

## Deliverables

- Passing targeted tests.
- Passing web project build.
- Browser screenshots for summary, role drilldown, agent picker, details dialog, and tooltip.
- Browser proof JSON with dimensions and overflow/layering assertions.
- Updated execution report and raw-note closure.
- Completed-stage validator pass.

## Dependency Impact

- Final gate. Reopen prior subbundles if proof contradicts any request.

## Validation Depth

- Component tests plus real browser proof.
- Large desktop viewport and narrower layout check when practical.

## Implementation Steps

1. Run targeted component tests.
2. Run web project build.
3. Start or reuse the local dev server.
4. Drive the project-structure Start flow in browser.
5. Capture required screenshots and proof JSON.
6. Update bundle report, README, and subbundle statuses.
7. Run completed-stage validator.

## Scope Exceptions

- None. Any missing proof must be recorded as a blocker or reopened work, not hidden as residual risk.

## Do Not Do

- Do not mark browser proof complete without screenshots.
- Do not ignore horizontal overflow, clipped badges, or buried nested dialogs.
- Do not close the bundle while any requested note remains pending without an explicit blocker.

## Acceptance Checklist

- All tests/build commands pass or failures are honestly documented.
- Summary view matches the full-width design direction.
- Role view shows candidate ranking and plus card.
- Picker shows search, tags, favorites.
- Badge tooltip and details dialog are visible and readable.
- Raw notes are closed one by one.

## Proof Required

- Test output.
- Build output.
- Screenshot paths.
- Browser proof JSON.
- Completed-stage validator output.

## Browser Validation Logging

- Record route, viewport, action sequence, DOM assertions, overflow checks, screenshot paths, and pass/fail in `reviews/01-execution-report.md`.

## Progression Gate

- Pass only when every raw note is `Solved` or a concrete blocker/follow-up is documented.

## Suggested Agent Prompt

Execute subbundle 04 only. Run validation, capture browser proof, update closure records, and run the completed-stage validator.
