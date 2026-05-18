# 03-validation-and-closure

## Status

- `Completed`

## Objective

- Prove both UI changes against the raw notes, update bundle evidence, and close or honestly block every note.

## Covered Inputs

- All raw notes `N001` through `N005`.
- `R001` through `R006`.

## Prerequisites

- `01-01-tab-header-density` is completed or explicitly blocked.
- `02-02-sidebar-overflow-continuation-menu` is completed or explicitly blocked.
- Tailwind source changes are known and ready for regeneration.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\shell-tab-menu-density\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\codex\bundles\shell-tab-menu-density\traceability\01-requirement-traceability.md
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\wwwroot\css\output.css
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AppTabStripTests.cs

## Deliverables

- Tailwind output regenerated.
- Targeted tests/build commands recorded.
- Browser screenshots or explicit blocker recorded.
- Execution report synchronized with subbundle gates, browser analytics, raw-note closure, commands, and residual risks.
- Completed-stage validator run.

## Dependency Impact

- This is final closure. Weak evidence here must reopen `01` or `02` instead of burying risk.

## Validation Depth

- `End-to-end regression and closure`.

## Implementation Steps

1. Run Tailwind build after CSS changes.
2. Run targeted component tests or a broader build/test command if targeting is unavailable.
3. Start the app and perform large desktop browser proof with `more_up` open.
4. Perform a narrower-width browser check.
5. Update `reviews/01-execution-report.md` and raw-note closure rows.
6. Run completed-stage bundle validator.

## Scope Exceptions

- If browser tooling or app startup is blocked, document the exact blocker and retain the strongest available build/test evidence.

## Do Not Do

- Do not mark a UI note solved without either browser proof or an explicit validation blocker.
- Do not leave executed subbundles as `Ready` or `In progress`.

## Acceptance Checklist

- Commands and outcomes are recorded.
- Browser analytics rows are populated.
- Raw notes are marked `Solved`, `Partially solved`, or `Not solved`.
- Residual risks are concrete and not hiding missing proof.
- Completed-stage validator passes or blocker is explicit.

## Proof Required

- `npm --prefix Tailwind run build`.
- Targeted component tests for affected shell components.
- Browser proof screenshots for large desktop open state and narrower-width behavior.
- `validate_bundle.py --stage completed`.

## Browser Validation Logging

- Route: `/processes`.
- Viewports: large desktop and narrower width below the compaction breakpoint.
- Actions/assertions: inspect tab/status row, open continuation panel, inspect layering/clipping/card grid, inspect no sidebar internal nav scroll.
- Screenshot paths: store under `codex/bundles/shell-tab-menu-density/evidence/` when captured.
- Review questions: do the screenshots answer every raw note directly?

## Progression Gate

- The bundle can close only when code, CSS, tests/build, browser evidence, execution report, and final validator agree.

## Suggested Agent Prompt

```text
Validate and close the bundle. Rebuild styles, run targeted tests, capture browser proof for the large desktop tab row and open continuation menu, update all execution report rows and raw-note closure statuses, then run the completed-stage validator. Reopen earlier work if proof is weak.
```
