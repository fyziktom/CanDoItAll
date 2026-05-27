# SB17: Full Live Test Observability And Runbook

## Status

- Completed

## Objective

Prepare full live process observability and a user runbook.

## Covered Inputs

- RQ08: live Blazor/Tetris readiness.
- RQ10: final real-test runbook and release readiness report.

## Prerequisites

- SB16 runtime stabilization must be complete.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor
- repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs
- repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs

## Deliverables

- Runbook for importing/selecting live profile, starting a run, observing step 0, and continuing through implementation/QA/writeback.
- UI/API exposure for current step, required artifacts, validation details, pending approvals, tool receipts, diagnostics, and next recovery action.
- Playwright/component smoke proof for run-detail observability.

## Dependency Impact

- SB18 final red-team depends on runbook and observability proof.

## Validation Depth

- Critical semantic proof must show the operator can see validation details and next recovery action.

## Implementation Steps

- Audit live process observability surfaces.
- Add runbook and tests for missing observability items.
- Capture browser proof if UI surfaces change or are validated.
- Update `proof/SB17`.

## Do Not Do

- Do not replace browser proof with component-only proof when route behavior matters.
- Do not hide pending approvals or invalid artifact statuses from the operator.

## Acceptance Checklist

- Runbook exists.
- Observability surfaces expose required state.
- Component/browser smoke proof is captured.

## Proof Required

- Failing-first/adversarial transcript for missing observability.
- Passing component/browser transcript.
- Source assertions, anti-stub audit, hashes, and screenshots.

## Browser Validation Logging

- Record route, desktop viewport, Playwright actions, screenshots, and result for live process observability.

## Progression Gate

- SB18 may start only after runbook and observability proof are complete.

## Suggested Agent Prompt

Prepare the live-test runbook and prove operators can see current step, artifacts, validation, approvals, diagnostics, and recovery actions.

## Closure Proof

- bundle://proof/SB17/manifest.md
- bundle://proof/SB17/semantic-invariants.md

