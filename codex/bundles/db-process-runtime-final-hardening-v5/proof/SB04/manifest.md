# Proof manifest SB04

## Status

Completed.

## Owned requirements

- R5: Process side effects must be idempotent.
- R8: Broad validation caveats must be closed or classified.
- R9: Process DB tests must red-team canonicality.

## Semantic invariant contract

`bundle://proof/SB04/semantic-invariants.md`

## Idempotency matrix

`bundle://proof/SB04/idempotency-matrix.md`

## Changed files

| File | SHA-256 | Reason |
|---|---:|---|
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `590F0A5C9EB47723A0403A72A8E41593B6EB9F6A486B0942C42B8F32125179E3` | Adds stable activity idempotency keys and pending automation-dispatch dedupe. |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs` | `EDA0757FF6149E7BD6B316298048DA2DA574540B2C6802622776B5A7F2F625EE` | Adds duplicate side-effect tests for activity and automation dispatch. |
| `bundle://proof/SB04/idempotency-matrix.md` | n/a | Documents every process outbox side-effect idempotency contract. |

## Validation commands

| Command | Result | Transcript |
|---|---|---|
| Focused duplicate side-effect tests before production fix | Failed as expected, 2 tests | `bundle://proof/SB04/duplicate-side-effect-tests-failing-first.log` |
| Focused duplicate side-effect tests after production fix | Passed, 2 tests | `bundle://proof/SB04/duplicate-side-effect-tests.log` |
| Process outbox idempotency source audit | Passed | `bundle://proof/SB04/process-outbox-idempotency-source-audit.log` |

## Source assertions

- `ActivityWriteRequest.IdempotencyKey` is now stable for definition save, definition publish, and run start outbox payloads.
- `Activity_Entries.IdempotencyKey` already has a unique index and `ActivityService.RecordAsync(...)` suppresses duplicates.
- `SearchDocument` already has a unique `SourceType` + `SourceKey` index; upsert/delete are idempotent under that key.
- `EnqueueAutomationDispatchAsync(...)` dedupes matching pending dispatches from both the current change tracker and persisted pending outbox rows.
- Automation dispatch dedupe compares parsed `ProcessOutboxPayload.AutomationDispatch`, not raw JSON string fragments.

## Semantic adequacy

The shallow-pass trap was to rely on conditional outbox finalization and assume duplicate rows are rare. The failing-first tests show duplicate outbox rows duplicated activity and duplicate automation enqueue produced separate pending commands. The passing tests prove stable keys and pending dedupe close those paths.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ActivityEntry.IdempotencyKey` | Stable keys in `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | Unique index and duplicate suppression in `repo://src/CanDoItAll.Modules.Activity/ActivityModels.cs` | `bundle://proof/SB04/duplicate-side-effect-tests.log` | `bundle://proof/SB04/duplicate-side-effect-tests-failing-first.log` |
| `SearchDocument.SourceType` / `SourceKey` | Process outbox search payloads | Unique search index and upsert/delete semantics in `repo://src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs` | `bundle://proof/SB04/idempotency-matrix.md` | Existing retry tests in `ProcessOutboxIntegrationTests` |
| `ProcessOutboxPayload.AutomationDispatch` | `EnqueueAutomationDispatchAsync(...)` | Pending dedupe by run/step/trigger in `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `bundle://proof/SB04/duplicate-side-effect-tests.log` | `bundle://proof/SB04/duplicate-side-effect-tests-failing-first.log` |

## Residual risks

No outbox-level idempotency column was added. That is intentional for this phase because completed automation dispatch rows may be legitimate historical events; only duplicate pending commands are suppressed.
