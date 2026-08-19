# Operation, idempotency, recovery, and audit

## Lifecycle

`LlmChatOperationStatus`:

- `Pending`
- `Running`
- `Succeeded`
- `Failed`
- `CancellationRequested`
- `Cancelled`
- `RecoveryRequired`

Transitions are explicit and tested. Unknown enum values fail closed.

## Admission

The send command supplies a non-empty `LlmChatOperationId`. The HTTP adapter may map a validated
`Idempotency-Key` to this ID, but the module receives a strongly typed ID and request fingerprint.

Admission is transactional:

- insert when absent;
- return existing state when ID and fingerprint match;
- conflict when ID exists with a different fingerprint.

Only the caller that wins an atomic `Pending -> Running` dispatch claim may proceed. A retry observing
`Running`, `CancellationRequested`, `RecoveryRequired`, or a terminal state returns the existing
operation and never starts another provider call. The operation records durable evidence timestamps
for turn admission, provider-dispatch start/return, and transcript completion.

## Cancellation

Use both:

- durable cancellation-requested state in PostgreSQL;
- an in-process `CancellationTokenSource` registry for the currently executing operation.

This makes cancellation useful now and compatible with a future worker/multi-instance dispatcher.
If cancellation is requested from another instance and cannot interrupt the provider socket immediately,
the post-dispatch durable cancellation check still prevents semantic assistant completion. Immediate
cross-instance transport interruption remains a later dispatcher concern.

## Reconciliation cases

| Persisted evidence | Operation row | Result |
|---|---|---|
| assistant entry for operation turn ID | Running/Pending | mark Succeeded |
| active pending user entry, dispatch never started | Running | exact abandon, then mark Failed/Cancelled; never infer success |
| active pending user entry, dispatch may have started | Running | RecoveryRequired; no automatic redispatch |
| no transcript entry and dispatch never started | Running | mark Failed/Cancelled or explicitly re-admit under the same atomic claim policy |
| no transcript entry and dispatch may have started | Running | RecoveryRequired; no automatic redispatch |
| no transcript entry and invocation failure recorded | Running | mark Failed/Cancelled |
| operation succeeded but response was lost | Succeeded | return existing result |
| same ID, different request fingerprint | any | conflict, no invocation |

No background heuristic removes an active turn. Recovery requires the exact operation/turn ID, a
durable `RecoveryRequired` operation state, and proof that no live execution lease still owns the
operation. A running call must be cancelled and drained before abandonment is allowed.

## Usage audit

An invocation record is written for:

- success;
- provider failure with known usage;
- empty response after retry with known usage;
- deadline/cancellation with known usage where available;
- profile-fence failure after provider completion.

Each record also captures the nullable requested override and the effective effort resolved at
dispatch when known. `null` requested means provider default; it must not be conflated with explicit
`None`. The operation fingerprint includes the immutable revision settings fingerprint, which includes
the requested override.

Transcript usage remains attached to assistant entries. Invocation audit is authoritative for billed
attempt evidence, including failed turns.
