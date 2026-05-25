# Semantic invariants SB04

## Invariants to prove

- No stale worker may write canonical process DB state.
- Lease ownership must be explicit and verifiable.
- Retry behavior must be idempotent.
- PostgreSQL runtime must remain canonical.

## Negative proof

- `bundle://proof/SB04/duplicate-side-effect-tests-failing-first.log` captures two pre-hardening failures:
  - duplicate definition-save outbox records created duplicate activity entries,
  - duplicate automation dispatch enqueue created separate pending commands for the same run/step/trigger.

## Positive proof

- `bundle://proof/SB04/idempotency-matrix.md` defines the side-effect contract for `SearchUpsert`, `SearchDelete`, `Activity`, and `AutomationDispatch`.
- `bundle://proof/SB04/duplicate-side-effect-tests.log` proves stable activity idempotency keys and automation-dispatch pending dedupe.
- `bundle://proof/SB04/process-outbox-idempotency-source-audit.log` verifies the hardened code paths and existing unique indexes that back the contract.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ActivityEntry.IdempotencyKey` | Stable process outbox keys in `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | Unique index and duplicate suppression in `repo://src/CanDoItAll.Modules.Activity/ActivityModels.cs` | `bundle://proof/SB04/duplicate-side-effect-tests.log` | `bundle://proof/SB04/duplicate-side-effect-tests-failing-first.log` |
| `SearchDocument.SourceType` / `SourceKey` | Process outbox search payloads | Unique search index and upsert/delete semantics in `repo://src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs` | `bundle://proof/SB04/idempotency-matrix.md` | Existing retry coverage plus matrix review |
| `ProcessOutboxPayload.AutomationDispatch` | `EnqueueAutomationDispatchAsync(...)` | Pending dedupe by run/step/trigger in `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `bundle://proof/SB04/duplicate-side-effect-tests.log` | `bundle://proof/SB04/duplicate-side-effect-tests-failing-first.log` |
