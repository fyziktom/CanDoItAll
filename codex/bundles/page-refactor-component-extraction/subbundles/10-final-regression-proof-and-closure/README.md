# Final Regression Proof And Closure

## Status

- `Ready`

## Objective

- Prove the refactor preserved behavior, close every raw note, and pass completed-stage bundle validation.

## Covered Inputs

- `N005`
- `N006`
- `N007`
- `R011`

## Prerequisites

- All implementation subbundles are `Completed` or honestly `Blocked`.
- Workbook checklist statuses are current.
- Execution report has proof rows for every executed subbundle.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`
- `C:\repositories\CanDoItAll\codex\bundles\page-refactor-component-extraction\reviews\01-execution-report.md`

## Deliverables

- Targeted test and build proof.
- Browser proof and screenshots for changed routes.
- Workbook checklist finalized.
- Raw note closure rows marked `Solved`, `Partially solved`, or `Not solved`.
- Completed-stage validator pass or explicit blockers.

## Dependency Impact

- This is the final closure gate for the entire bundle.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Review all subbundle statuses and proof rows.
2. Run targeted tests for changed areas.
3. Run `dotnet build CanDoItAll.slnx`.
4. Capture browser proof for changed routes.
5. Update workbook and execution report.
6. Run completed-stage bundle validator.

## Scope Exceptions

- Any unavailable browser seed data must be documented as a blocker or follow-up subbundle, not residual risk.

## Do Not Do

- Do not mark final closure complete with pending proof rows.
- Do not collapse partial raw-note closure into solved.

## Acceptance Checklist

- No executed subbundle remains `Ready` or `In progress`.
- Execution report gate rows are populated.
- Browser analytics rows are populated for UI-relevant subbundles.
- Raw-note closure rows are final.
- Completed-stage validator passes.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `dotnet build CanDoItAll.slnx`
- Playwright/browser proof for changed routes.
- `C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --stage completed`

## Browser Validation Logging

- Routes: all changed route pages.
- Viewports: `1600x900` and selected narrow routes when layout changed.
- Required actions: route smoke plus changed interaction flows.
- Screenshots: all changed UI routes.

## Progression Gate

- Bundle can close only when completed-stage validator passes and every raw note has a defensible closure status.

## Suggested Agent Prompt

```text
Implement subbundle 10 only. Audit all prior proof, run final tests/build/browser proof, update workbook and execution report, close raw notes honestly, and run completed-stage validation.
```
