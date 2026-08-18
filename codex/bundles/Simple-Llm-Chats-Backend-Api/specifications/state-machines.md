# State machines

## Definition

```text
Draft ----activate----> Active
  |                       |
  | archive               | suspend
  v                       v
Archived <------------ Suspended
             archive
```

Allowed:

- Draft -> Active
- Draft -> Archived
- Active -> Suspended
- Active -> Archived
- Suspended -> Active
- Suspended -> Archived

Archived is terminal in this bundle. Reactivation from archive requires a later explicit product
decision.

Dispatch policy:

- Draft: no normal conversation creation or turn execution;
- Active: allowed;
- Suspended: read/history allowed, provider dispatch denied;
- Archived: read/history allowed, new conversation and provider dispatch denied.

## Conversation

```text
Active ----archive----> Archived
```

Archived is read-only. There is no public purge endpoint in this bundle.

## Operation

```text
Pending -> Running -> Succeeded
                  \-> Failed
                  \-> CancellationRequested -> Cancelled
                  \-> RecoveryRequired

Pending -> CancellationRequested -> Cancelled
Running -> RecoveryRequired
```

Execution evidence within `Running` advances monotonically:

```text
Admitted -> TurnAdmitted -> ProviderDispatchStarted -> ProviderDispatchReturned -> TranscriptCompleted
```

The coarse status and the evidence checkpoints are separate: status is the public lifecycle, while the
checkpoints make crash reconciliation deterministic. A missing checkpoint must never be guessed.

Rules:

- terminal states are immutable except an evidence-backed reconciliation from
  `RecoveryRequired` to `Succeeded`, `Failed`, or `Cancelled`;
- same operation ID never creates a second logical invocation;
- cancellation requested before assistant commit prevents assistant completion;
- raw provider exceptions never become operation failure text;
- profile switch may leave the originating operation `RecoveryRequired` when the old profile cannot be
  safely finalized.

## Active generic turn

The generic transcript remains authoritative:

```text
Idle -> PendingUserPersisted -> AssistantCompleted -> Idle
                         \-> ExactCompensation -> Idle
                         \-> Crash -> ExplicitRecoveryRequired
```

No timer-based heuristic deletes an active turn.
