# Operation state machine and reconciliation

The exact enum names may evolve, but semantic states must distinguish:

```text
Accepted -> Queued -> Claimed -> Admitted -> Streaming/Running
    -> Succeeded
    -> Failed
    -> Cancelled
    -> RecoveryRequired
```

`CancellationRequested` is durable evidence/phase and may also be a state, but it must never allow a
later success when its generation/timestamp wins finalization ordering.

## State invariants

- One operation ID maps to one immutable request fingerprint.
- One operation has at most one active execution claim epoch.
- One conversation has at most one admitted active turn unless explicit future parallelism is designed.
- Possible dispatch with missing terminal evidence never automatically redispatches.
- A terminal operation has no live active turn.
- Succeeded has one canonical assistant message and final usage.
- Failed/Cancelled has no completed assistant transcript message.
- RecoveryRequired names the unresolved evidence boundary.
- Terminal replay does not depend on current definition/conversation status.

## Reconciliation inputs

The reducer receives durable facts only:

- operation/request fingerprint;
- state and revision;
- claim owner/epoch/lease expiry;
- active-turn identity;
- provider attempt start/first-delta/terminal evidence;
- assistant commit evidence;
- cancellation generation/time;
- profile identity/generation;
- compensation result.

Process-local task presence is never authoritative.

## Conservative uncertain outcome

When dispatch may have occurred and no trustworthy terminal result exists, reconciliation must not issue
another paid dispatch. It enters RecoveryRequired or a named operator-resolvable state.
