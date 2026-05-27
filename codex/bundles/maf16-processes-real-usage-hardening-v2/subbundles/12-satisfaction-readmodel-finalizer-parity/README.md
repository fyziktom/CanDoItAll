# SB12: Satisfaction Read Model And Finalizer Parity

## Status

- Completed

## Objective

Ensure read model and finalizer agree on required artifact status.

## Covered Inputs

- RQ06: ensure artifact satisfaction read model and finalizer validation agree.

## Prerequisites

- SB11 content hash and storage reference proof must be complete.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeReadQueryServiceTests.cs

## Deliverables

- Shared validation path or proof of parity between read model and finalizer.
- API/UI-readable partial statuses such as `ContentUnavailable`, `WrongProducerMode`, and `StaleOrWrongRun`.

## Dependency Impact

- SB13 recovery and SB17 observability depend on accurate status projection.

## Validation Depth

- Critical semantic proof must fail if step detail says satisfied while finalizer rejects the same artifact.

## Implementation Steps

- Audit read-model satisfaction computation.
- Refactor to shared validator or add parity enforcement.
- Update health missing artifact counts.
- Update `proof/SB12`.

## Do Not Do

- Do not duplicate satisfaction logic with divergent status meanings.
- Do not hide invalid current-step artifacts from health counts.

## Acceptance Checklist

- Read model and finalizer agree on positive and invalid cases.
- Partial statuses surface through API/UI model if affected.
- Focused tests pass.

## Proof Required

- Failing-first parity transcript.
- Passing read-query/finalizer transcript.
- Source assertions, anti-stub audit, hashes, and browser proof if UI changes.

## Browser Validation Logging

- Record route, viewport, Playwright evidence, screenshots, and result if UI status rendering changes.

## Progression Gate

- SB13 may start only after parity proof is captured.

## Suggested Agent Prompt

Unify or prove artifact satisfaction parity between the read model and finalizer, including invalid current-step artifact states.

## Closure Proof

- bundle://proof/SB12/manifest.md
- bundle://proof/SB12/semantic-invariants.md

