# SB04: Large-screen UI launch-to-completed-run and operator readback

## Status
Prepared.

## Objective
Rerun and, if needed, repair the large-screen project/project-structure launch flow through completed run, artifacts, and runtime-host readback.

## Exact Source References
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs`
- `repo://src/CanDoItAll.Web`
- `repo://src/CanDoItAll.Modules.Processes`

## Implementation Steps
- Rerun project/project-structure Playwright launch-to-completed-run test at 1900x1200.
- Confirm completed status is visible.
- Confirm evidence/artifact tab shows artifact records.
- Confirm run steps dialog shows completed/skipped steps.
- Confirm runtime-host readback panel is visible or record exact remaining UI gap.

## Do Not Do
- Do not extract dispatcher/process runtime core into a new package.
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not weaken Process Core genericity.
- Do not create proof-heavy churn.

## Acceptance Checklist
- UI launch from project-structure passes.
- Completed run is visible in browser.
- Artifacts/evidence are visible.
- Runtime-host readback is operator-visible or explicit blocker is recorded.

## Proof Required
- Playwright transcript.
- Screenshots of launch, assignment review, completed summary, artifacts, completed steps, runtime-host readback if applicable.

## Browser Validation Logging
Required: large desktop 1900x1200.

## Progression Gate
SB05 may start after UI path is green or blocker is exact.
