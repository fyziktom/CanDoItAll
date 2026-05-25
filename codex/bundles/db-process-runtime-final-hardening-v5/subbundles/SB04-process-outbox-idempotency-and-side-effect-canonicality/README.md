# SB04 - Process outbox idempotency and side-effect canonicality

## Status

Completed - critical foundation.

## Objective

Prove and harden idempotency for all process outbox side effects.

## Covered inputs

- Process outbox finalization is now conditional.
- Side effects may still occur before finalization fails due to lease loss.
- At-least-once delivery requires idempotent side effects.

## Exact source references

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Infrastructure/Search/*`
- `repo://src/CanDoItAll.Infrastructure/Logging/*`
- `repo://src/CanDoItAll.Modules.Activity/*`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/*`

## Deliverables

1. Build an idempotency matrix for every `ProcessOutboxPayload` side effect:
   - `SearchUpsert`
   - `SearchDelete`
   - `Activity`
   - `AutomationDispatch`
2. Add stable idempotency/dedupe keys where missing.
3. Add duplicate/retry tests:
   - side effect runs once logically even if outbox lease is lost after side effect,
   - retry does not duplicate activity events,
   - automation dispatch enqueue is deduped for the same run/step/trigger/outbox command.
4. Decide whether `ProcessOutboxRecord` needs an explicit `IdempotencyKey`/`DedupeKey`.

## Implementation steps

- Inspect each side-effect consumer.
- Add stable idempotency keys to payload or outbox record if needed.
- Add uniqueness constraints if appropriate.
- Create tests for at-least-once replay and duplicate suppression.

## Do not do

- Do not assume "retry is rare".
- Do not allow duplicate activity/search/process dispatch side effects just because finalization is conditional.
- Do not put idempotency only in comments.

## Acceptance checklist

- [x] Every side-effect type has an explicit idempotency contract.
- [x] Duplicate execution negative tests exist.
- [x] Automation dispatch side effect cannot create unbounded duplicate pending dispatch records.
- [x] Documentation explains at-least-once semantics.

## Implementation summary

- Added stable process activity idempotency keys for definition save, definition publish, and run start outbox payloads.
- Preserved legacy activity fallback idempotency by outbox record id for older payloads.
- Added tracked and persisted pending automation-dispatch dedupe keyed by definition/run/step/normalized trigger.
- Documented the side-effect matrix and the decision not to add a broad `ProcessOutboxRecord.IdempotencyKey` column for completed historical records.
- Added failing-first and passing duplicate side-effect integration tests.

## Proof required

- `proof/SB04/manifest.md`
- `proof/SB04/idempotency-matrix.md`
- `proof/SB04/duplicate-side-effect-tests.log`

## Browser validation logging

N/A.

## Progression gate

SB05/SB06 should not claim throughput quality until duplicate side effects are safe.

## Suggested agent prompt

Implement SB04. Build and prove idempotency for every process outbox side effect under lease-loss and retry scenarios.
