# 04-regression-and-closure

## Status

- `Completed`

## Objective

Run final targeted validation, audit the raw request note by note, and synchronize bundle state with implemented proof.

## Covered Inputs

- `N001`
- `N002`
- `N003`
- `N004`

## Prerequisites

- `01-project-database-transfer` completed.
- `02-project-zip-package-import-export` completed.
- `03-ui-exposure-and-workflow-proof` completed with browser analytics.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`
- `C:\repositories\CanDoItAll\project_import_export_transfer_bundle\reviews\01-execution-report.md`

## Deliverables

- Targeted test and build results recorded.
- Browser validation analytics reviewed.
- Raw note closure table updated to `Solved`, `Partially solved`, or `Not solved`.
- Final bundle status synchronized.

## Dependency Impact

- This is the closure gate.
- Weak proof here reopens the relevant earlier subbundle.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run targeted integration tests for database transfer and package import/export.
2. Run targeted component tests for Projects page and transfer UI.
3. Run a build if targeted tests do not compile all edited projects.
4. Review browser screenshots and analytics from subbundle `03`.
5. Update raw note closure with proof references.
6. Run prepared/completed bundle validators as required.

## Scope Exceptions

- None expected. Any partial result must reopen the owning subbundle.

## Do Not Do

- Do not mark closure complete with pending browser analytics.
- Do not hide missing zip import proof as residual risk.

## Acceptance Checklist

- All requested modes have proof.
- Existing transfer behavior did not regress.
- Bundle docs match actual implementation and validation.
- Final closure validator passes or any blocker is explicitly documented with a reopened phase.

## Proof Required

- Final targeted commands and outcomes in execution report.
- Browser analytics and screenshot review recorded.
- Raw note closure statuses updated.
- `scripts/validate_bundle.py --stage completed` outcome recorded.

## Browser Validation Logging

- Review existing `03` rows. No new route required unless prior browser proof was weak.

## Progression Gate

- Passed. Code, targeted tests, browser proof, raw-note closure, and completed-stage bundle validation agree.

## Suggested Agent Prompt

```text
Execute closure only: run final targeted validation, audit the raw notes, update bundle proof, and run final validators. Reopen earlier subbundles if any required proof is missing.
```
