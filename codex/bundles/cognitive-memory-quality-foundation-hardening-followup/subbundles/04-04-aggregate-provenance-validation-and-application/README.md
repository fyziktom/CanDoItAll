# 04-aggregate-provenance-validation-and-application

## Status

- `Completed`

## Objective

Harden aggregate candidate creation, validation, and application so generated aggregate memories are genuinely grounded, policy-safe, and idempotent.

## Success Criteria

- Aggregate canonical/summary text is cluster-level synthesis rather than a raw per-record dump.
- Every aggregate claim has source maps that can be traced to source memory, source item when available, and evidence anchor when available.
- Validation covers contradiction relations, attacking evidence, stale/superseded/rejected sources, generated-only evidence, weak source coverage, restricted/redacted content, and access policy.
- Applying an approved aggregate is idempotent and cannot create duplicate memory records under repeat or concurrent calls.
- Application supports legitimate source-memory/evidence provenance and does not fail only because a source item is absent when the evidence anchor is otherwise valid.

## Covered Inputs

- H-08, H-09, H-10, H-15.

## Prerequisites

- Subbundle 01 complete.
- Subbundle 02 complete and Gate B passed.
- Subbundle 03 complete and Gate C passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryDreamConsolidationService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryDreamValidator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryAggregateMemoryApplicator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualitySupport.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntityConfigurations.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryQualityFoundationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryQualityPersistenceModelTests.cs`

## Deliverables

- Aggregate synthesis helper or service boundary with deterministic testable behavior.
- Expanded validation issue tests and decision tests.
- Aggregate application idempotency/race tests.
- Provenance tests for source maps, claim evidence links, record evidence links, and source links.

## Dependency Impact

- Recall synthesis and final corpus proof depend on aggregate records being trustworthy. Weak aggregate validation invalidates Subbundle 05 and Subbundle 07.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Use Subbundle 01 tests to confirm current aggregate text/provenance gaps.
2. Refactor candidate text creation into a focused deterministic component or internal helper.
3. Ensure restricted/redacted source material is not copied into aggregate text.
4. Expand validation to use both claim source maps and source memory relation/stability state where relevant.
5. Harden apply idempotency around `MemoryRecordId`, content hash, and repeated apply calls.
6. Add integration tests for persistence side effects and provenance completeness.

## Scope Exceptions

- Do not add human review UI. Existing review-item records are enough for this subbundle.
- Do not require an external LLM provider for aggregate synthesis tests.

## Do Not Do

- Do not mark weak, restricted, or contradictory aggregates as approved just because source maps exist.
- Do not hide missing provenance as a warning if the aggregate would be activated.
- Do not duplicate aggregate memories on replay.

## Acceptance Checklist

- Aggregate synthesis tests prove no raw dump and no restricted text leakage.
- Validation issue matrix tests pass.
- Apply idempotency and provenance tests pass.
- Existing happy-path aggregate tests still pass.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryQualityFoundationTests" --logger "console;verbosity=minimal" -m:1`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryQualityPersistenceModelTests" --logger "console;verbosity=minimal" -m:1`
- Execution report includes the validation issue matrix and aggregate apply idempotency result.

## Browser Validation Logging

- N/A. This subbundle is API/domain/persistence behavior only.

## Progression Gate

- Subbundle 05 may start only after aggregate text, validation, and apply idempotency are proven policy-safe and provenance-complete.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Harden aggregate candidate text, validation, and application idempotency. Keep synthesis deterministic for tests and record validation/provenance proof in the execution report.
```
