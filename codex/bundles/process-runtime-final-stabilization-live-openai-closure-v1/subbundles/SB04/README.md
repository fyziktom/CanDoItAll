# SB04: Large-screen UI launch-to-completed-run and operator readback

## Status
- Current status: Completed

## Objective
Rerun and, if needed, repair the large-screen project/project-structure launch flow through completed run, artifacts, and runtime-host readback.

## Covered Inputs
- RN-001: Check whether processes now work like before.
- RN-004: Stabilize process functionality before further runtime extraction.

## Prerequisites
- SB03 closure gate must be green or must record an exact functional blocker.
- Playwright/browser infrastructure must be available or blocker must be explicit.

## Exact Source References
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs`
- `repo://src/CanDoItAll.Web`
- `repo://src/CanDoItAll.Modules.Processes`

## Deliverables
- Playwright transcript at 1900x1200.
- Screenshots for launch, assignment review, completed summary, artifacts/evidence, completed/skipped steps, and runtime-host readback classification.
- Source/test repair only if browser proof exposes a real UI defect.

## Dependency Impact
- SB05 may start after UI path is green or the UI blocker is exact.
- SB06 final decision depends on the browser analytics row and screenshot review.

## Validation Depth
- Entry gate: confirm SB03 result and Playwright source references.
- Closure gate: Playwright transcript, screenshots, screenshot review, execution-report browser analytics, and proof manifest.
- Semantic Adequacy Gate: record shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note closure in `bundle://proof/SB04/semantic-invariants.md`.

## Implementation Steps
- Rerun project/project-structure Playwright launch-to-completed-run test at 1900x1200.
- Confirm completed status is visible.
- Confirm evidence/artifact tab shows artifact records.
- Confirm run steps dialog shows completed/skipped steps.
- Confirm runtime-host readback panel is visible or record exact remaining UI gap.

## Scope Exceptions
- None planned. Missing browser proof is a blocker for UI release readiness.

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
- `bundle://proof/SB04/manifest.md` with changed-file hashes and portable artifact references.
- `bundle://proof/SB04/semantic-invariants.md` with invariant IDs cited by transcripts.

## Browser Validation Logging
- Required: large desktop 1900x1200 route and screenshots recorded in `bundle://reviews/01-execution-report.md`.

## Progression Gate
- SB05 may start after UI path is green or blocker is exact.

## Suggested Agent Prompt
- Run the project/project-structure Playwright launch-to-completed-run proof at 1900x1200. Review screenshots for completed status, artifacts, steps, and runtime-host readback.
