# SB13: Recovery Manager And Operator Approval Correctness

## Status

- Completed

## Objective

Fix and prove manager recovery and operator approval semantics.

## Covered Inputs

- RQ07: ensure recovery and operator approval cannot fake required artifact satisfaction.

## Prerequisites

- SB12 satisfaction/read-model/finalizer parity must be complete.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessStepRecoveryOptionContractTests.cs

## Deliverables

- Tests proving operator decision artifacts cannot satisfy original required deliverables unless explicitly mapped.
- Recovery artifact lineage/content-hash proof.
- Clear pending approval process state.

## Dependency Impact

- SB14 live preflight and SB17 observability depend on correct recovery and pending approval state.

## Validation Depth

- Critical semantic proof must include valid recovery artifact and invalid operator-decision substitute.

## Implementation Steps

- Audit recovery and manager approval flows.
- Add tests for valid recovery and invalid decision substitution.
- Fix lineage/status handling if needed.
- Update `proof/SB13`.

## Do Not Do

- Do not treat approval decisions as deliverable evidence by default.
- Do not leave runs in mixed active/failed states after pending approval.

## Acceptance Checklist

- Operator decision substitute is rejected.
- Valid recovery evidence satisfies the original expectation only with correct lineage/content.
- Pending approval state is clear and persisted.

## Proof Required

- Failing-first operator-decision transcript.
- Passing recovery transcript.
- Source assertions, anti-stub audit, and hashes.

## Browser Validation Logging

- Record route, viewport, Playwright evidence, screenshots, and result if manager chat or approval UI changes.

## Progression Gate

- SB14 and SB17 may start only after recovery/approval semantics are proven.

## Suggested Agent Prompt

Prove recovery artifacts and operator approvals have separate semantics, with lineage and content hash required for deliverable satisfaction.

## Closure Proof

- bundle://proof/SB13/manifest.md
- bundle://proof/SB13/semantic-invariants.md

