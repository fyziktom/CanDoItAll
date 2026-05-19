# Provider Failure Runbooks

## Status

- `Completed`

## Objective

- Prove projection/provider failures are observable and documented, including an executable live-provider/Qdrant validation runbook.

## Covered Inputs

- CM-P1-002
- CM-P1-007

## Prerequisites

- API contract versioning subbundle passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Operations\CognitiveMemoryProjectionRebuildService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Projection\CognitiveMemoryProjectionAdapters.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Projection\CognitiveMemoryProjectionAdapterContracts.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryOperationalServicesTests.cs
- C:\repositories\CanDoItAll\docs\cognitive-memory\operations\validation-and-testing.md

## Deliverables

- Deterministic provider failure tests.
- Runbook for live Qdrant/provider validation with environment prerequisites.
- Docs describing failure observation and recovery path.

## Dependency Impact

- Operator audit and docs closure depend on failure states being queryable and documented.

## Validation Depth

- Operational correctness gate.

## Implementation Steps

1. Inspect current projection failure behavior.
2. Add failure-path proof without hiding adapter exceptions.
3. Document live-provider setup and expected assertions.
4. Update execution report.

## Do Not Do

- Do not require live Qdrant for normal unit tests.
- Do not swallow provider failures or mark failed projections as healthy.

## Acceptance Checklist

- Failure leaves durable status/failure information.
- Local deterministic tests prove failure behavior.
- Live validation runbook is executable and honest about prerequisites.

## Proof Required

- Targeted unit/integration tests.
- Docs/runbook update.

## Proof Captured

- `ProjectionRebuildService_RecordsProviderFailureAndKeepsProjectionRebuildable` proves blocked run status, failed projection state, preserved `RebuildRequired`, and stored failure code/message.
- `docs/cognitive-memory/operations/provider-failure-runbook.md` documents live Qdrant/provider validation as an environment-gated beta proof.
- Unit Cognitive Memory and agent-context filter passed 142/142.

## Browser Validation Logging

- Browser proof is not required unless this subbundle changes rendered UI.

## Progression Gate

- Continue only after failure behavior is observable and documented.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Add deterministic projection/provider failure proof and an executable runbook, keep live infrastructure optional for normal tests, update proof rows, and stop if failure state is not durably observable.
```
