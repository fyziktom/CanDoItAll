# SB15 Progress-Sensitive Retry Classification

## Status

- `Completed`

## Objective

Stop blind current-step retries after one retry reproduces any stable blocker identity, even when unrelated diagnostics are added, removed, or reordered.

## Covered Inputs

- `bundle://inputs/04-persistent-repair-and-four-app-e2e-request.md`
- Production run `7d32cae3-1dca-45e7-9014-3e7da9ffa1ae` recovery receipts.

## Prerequisites

- SB14 evidence remains available as the prior baseline.
- Existing generic recovery tests pass before the failing-first addition.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRecoveryClassifier.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRecoveryClassifierTests.cs`

## Deliverables

- Stable generic diagnostic-identity comparison across prior same-step retry receipts.
- Default policy allowing at most one blind retry for a repeated blocker.
- Recovery receipts/instructions that explain persistent-blocker count without domain vocabulary.

## Dependency Impact

- No project-reference change.
- Runtime continues to depend only on process abstractions and driver diagnostic contracts.
- Serialized receipt compatibility is preserved unless an additive observable field is justified and tested.

## Implementation Steps

1. Add failing-first tests for a persistent diagnostic plus incidental diagnostic churn.
2. Define stable generic diagnostic identity and recurrence semantics.
3. Apply the bounded one-retry policy while retaining the global progress budget.
4. Update generic recovery receipt/instruction wording and compatibility tests.
5. Run focused tests, source assertions, and architecture review.

## Validation Depth

- Critical isolated unit tests plus runtime/application regression tests.

## C# Architecture Impact

- Focused change inside the existing recovery classifier and receipt contract only if additional observable fields are necessary.
- No new service, project, partial class, static domain helper, or adapter responsibility.

## Pattern Decision

- PSR-06.

## Do Not Do

- Do not parse diagnostic summaries for domain words.
- Do not add UI, file, spreadsheet, .NET, or software-delivery concepts to runtime.
- Do not lower only the global retry budget.

## Acceptance Checklist

- Persistent diagnostic plus incidental churn routes manager after one retry.
- Replaced diagnostics retain bounded repair opportunity.
- Existing unsafe/policy/capability behavior is preserved.
- Generic source scan passes.

## Proof Required

- `bundle://proof/SB15/manifest.md`
- `bundle://proof/SB15/semantic-invariants.md`
- Failing-first and passing test transcripts, source assertions, changed-file hashes, and anti-stub audit.

## Browser Validation Logging

- Not applicable; this phase is deterministic runtime policy with no UI surface.

## Progression Gate

- SB16 may start only after the generic classifier behavior passes isolated tests and architecture scan.

## Suggested Agent Prompt

Implement generic persistent-diagnostic retry classification from failing-first fixtures, preserve dependency direction, and stop after one blind retry reproduces a stable blocker identity.
