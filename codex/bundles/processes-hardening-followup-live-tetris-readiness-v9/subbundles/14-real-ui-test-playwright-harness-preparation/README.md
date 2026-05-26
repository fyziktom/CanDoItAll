# SB14: Real UI Test Playwright Harness Preparation

## Status

- Status: `Completed`

## Objective

Prepare a Playwright test or manual runbook that selects the generic Blazor WASM PWA live profile, starts a fresh run, inspects step boundaries, assigns agents, observes progress, and validates artifacts.

## Covered Inputs

- RQ06 live-run UI/API readiness.
- RQ09 fake-completed baseline confusion.
- RQ10 final runbook input.

## Prerequisites

- SB13 generic regression is complete.

## Exact Source References

- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessManagementBundle.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessOperationContract.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RuntimeOperations.cs`
- `repo://Templates/Processes/seed-catalog`

## Deliverables

- Playwright harness or runbook for generic live profile selection and fresh run start.
- Browser validation analytics instructions for route, viewport, actions, assertions, screenshots, and result.

## Dependency Impact

- SB15 and SB16 use this harness/runbook for final readiness and red-team closure.

## Validation Depth

- UI-heavy. Use Playwright/browser proof when a runnable local target is available; otherwise record explicit blocker and API-level proof.

## Implementation Steps

1. Identify existing Playwright process workspace smoke tests.
2. Add or update a generic live profile preflight test/runbook.
3. Assert fresh run state has no seeded completed transitions or artifacts.
4. Record browser analytics if UI is exercised.

## Do Not Do

- Do not start from a seeded completed regression scenario.
- Do not hardcode any app topic into the harness.

## Acceptance Checklist

- Harness/runbook starts from generic live profile.
- Step boundaries and assignments are inspectable.
- Browser proof requirements are explicit.
- Fresh run state is not pre-completed.

## Proof Required

- `proof/SB14/manifest.md`
- `proof/SB14/semantic-invariants.md`
- `proof/SB14/transcripts/passing.txt`
- `proof/SB14/transcripts/source-assertions.txt`

## Browser Validation Logging

- Record Processes route, desktop viewport first, actions, assertions, screenshots, console state when applicable, and result in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- SB15 may start after the harness/runbook is available and fake-completed baseline confusion is explicitly tested or blocked.

## Suggested Agent Prompt

Prepare generic Playwright or manual UI runbook proof for starting a fresh Blazor WASM PWA live run from a reusable profile.
