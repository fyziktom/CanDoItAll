# 03-dream-run-lifecycle-and-mode-policies

## Status

- `Completed`

## Objective

Make explicit dream runs honest: lifecycle state must be transactional and failure-aware, dry runs must not write durable state, and every explicit consolidation mode must have a typed policy or predictable unsupported-mode behavior.

## Success Criteria

- Dream-run failures update `CognitiveMemoryDreamRunRecord.Status`, `FailureCode`, `FailureMessage`, and completion time predictably.
- Partial writes do not look like successful dream work.
- `PersistChanges = false` either performs a no-write dry run or is removed/renamed with tests proving the new contract.
- Every `CognitiveMemoryConsolidationMode` except `IncrementalRecent` has explicit supported or unsupported behavior.
- Idempotent replay returns durable existing candidates without revalidating or duplicating records.

## Covered Inputs

- H-05, H-06, H-07, H-13, H-15.

## Prerequisites

- Subbundle 01 complete.
- Subbundle 02 complete and Gate B passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryDreamConsolidationService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryClusterPlanner.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryDreamValidator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationContracts.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryQualityFoundationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryQualityPersistenceModelTests.cs`

## Deliverables

- Internal dream mode policy representation using typed modes and named reason codes.
- Lifecycle/failure handling with explicit persisted state.
- Dry-run behavior with proof of no durable writes, or a deliberate contract change.
- Tests for supported modes, unsupported modes, idempotent replay, and failure injection.

## Dependency Impact

- Aggregate validation and end-to-end proof depend on dream runs reporting truthfully. If this phase is weak, later successful aggregate tests can hide partial or wrong dream execution.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Reproduce dry-run and failure-path gaps from Subbundle 01 tests.
2. Introduce the smallest internal mode-policy abstraction or typed helper that removes broad default behavior.
3. Wrap dream-run mutation flow in a transaction or equivalent explicit consistency boundary.
4. Persist failed state with masked, actionable failure details when downstream work throws.
5. Implement or rename `PersistChanges = false` semantics and update contracts/tests accordingly.
6. Ensure idempotent replay does not create duplicate validation records or review items.

## Scope Exceptions

- Do not build scheduler/background automation in this subbundle.
- Do not add economic governance behavior.

## Do Not Do

- Do not leave `_ => true` mode behavior for explicit dream modes.
- Do not swallow exceptions without marking run state.
- Do not log or persist sensitive raw content in failure messages.

## Acceptance Checklist

- Failure-path test passes and persisted run state is `Failed`.
- Dry-run/no-write or renamed-contract test passes.
- All explicit modes have supported/unsupported tests.
- Replay test proves no duplicate downstream writes.

## Proof Required

- Targeted unit tests for mode policies and dry-run behavior.
- Targeted integration tests for failure state and idempotent replay.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryQualityFoundationTests" --logger "console;verbosity=minimal" -m:1`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryQualityPersistenceModelTests" --logger "console;verbosity=minimal" -m:1`

## Browser Validation Logging

- N/A. This subbundle is API/domain/persistence behavior only.

## Progression Gate

- Subbundle 04 may start only after dream runs are proven idempotent, failure-aware, and explicit about mode support.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Harden dream-run lifecycle, dry-run semantics, idempotency, and mode policies. Keep errors explicit and masked. Record proof and stop if any mode remains implicit.
```
