# Runtime Dispatcher And State Machines

## Design Intent

Runtime owns state transitions, scheduling, budgets, events, and terminal semantics. Dispatcher owns safe execution claims and strategy invocation. Manager owns interpretation and recovery decisions. These responsibilities must remain separate.

The old central dispatcher collapsed these responsibilities. The target runtime must make every transition observable, idempotent, and testable.

## Model Concepts

Primary runtime concepts:

- `ProcessRuntimeState`: run-level status, active budgets, terminal reason, cancellation marker.
- `StepRuntimeState`: step status, readiness, attempt count, active claim, block/incident reference.
- `DispatchWorkItem`: immutable request to execute a step strategy.
- `DispatchClaim`: lease record with owner, token, expiration, heartbeat, and result idempotency key.
- `StrategyResultEnvelope`: normalized output from strategy execution.
- `RuntimeTransition`: validated command to mutate runtime state.
- `ProcessRuntimeEventEnvelope`: append-only event emitted for state changes and decisions.

Runtime event envelope:

```csharp
public sealed record ProcessRuntimeEventEnvelope(
    Guid EventId,
    Guid RootRunId,
    Guid RunId,
    Guid? StepId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    Guid CorrelationId,
    Guid? CausationId,
    int PayloadSchemaVersion,
    string PayloadJson,
    string Sensitivity);
```

## Process Run State Machine

| From | To | Owner | Required event | Idempotency |
| --- | --- | --- | --- | --- |
| Created | Active | Runtime | `ProcessRunActivated` | Duplicate activation ignored if plan hash matches. |
| Active | Waiting | Runtime | `ProcessRunWaiting` | Recomputed from ready/blocked/running steps. |
| Waiting | Active | Runtime | `ProcessRunResumed` | Requires ready step, manager decision, or external input. |
| Active | Blocked | Runtime/manager | `ProcessRunBlocked` | Same incident fingerprint updates existing incident. |
| Blocked | Active | Runtime/manager | `ProcessRunUnblocked` | Requires resolved incident or waiver event. |
| Active | Completed | Runtime | `ProcessRunCompleted` | Terminal transition; duplicate completion returns current terminal state. |
| Active | Failed | Runtime | `ProcessRunFailed` | Terminal transition with failure classification. |
| Active | CancelRequested | Application/runtime | `ProcessRunCancelRequested` | Duplicate cancellation request updates audit metadata only. |
| CancelRequested | Cancelled | Runtime | `ProcessRunCancelled` | Terminal after running claims drain or cancel. |
| Active | Escalated | Manager | `ProcessRunEscalated` | Requires escalation owner and incident reference. |
| Escalated | WaitingForUser | Runtime | `ProcessRunWaitingForUser` | Requires user-actionable incident. |
| WaitingForUser | Active | Runtime/manager | `ProcessRunUserResponseAccepted` | Requires validated user response. |

## Step State Machine

| From | To | Owner | Required event | Idempotency |
| --- | --- | --- | --- | --- |
| Planned | Pending | Runtime bootstrap | `StepPendingCreated` | One per step instance. |
| Pending | Ready | Runtime scheduler | `StepReady` | Requires dependencies and required artifact slots. |
| Ready | WaitingApproval | Runtime/manager | `StepWaitingApproval` | Requires approval policy. |
| WaitingApproval | Ready | Application/runtime | `StepApprovalGranted` | Approval token is idempotency key. |
| Ready | Claimed | Dispatcher | `StepClaimed` | Requires active claim token and unexpired lease. |
| Claimed | Running | Dispatcher | `StepRunning` | Requires claim token match. |
| Running | Completed | Runtime | `StepCompleted` | Strategy result idempotency key prevents duplicate completion. |
| Running | Blocked | Runtime/manager | `StepBlocked` | Incident fingerprint deduplicates repeats. |
| Blocked | Ready | Runtime/manager | `StepUnblocked` | Requires resolved incident, recovered artifact, or approved waiver. |
| Running | Failed | Runtime | `StepFailed` | Terminal unless recovery creates a new attempt. |
| Running | Cancelled | Runtime | `StepCancelled` | Requires run cancellation or step cancellation policy. |
| Ready | Skipped | Runtime/manager | `StepSkipped` | Requires branch route or policy decision. |

## Dispatch Claim State Machine

| From | To | Owner | Required event | Idempotency |
| --- | --- | --- | --- | --- |
| Unclaimed | Claimed | Dispatcher | `DispatchClaimCreated` | Unique claim token. |
| Claimed | LeaseRenewed | Dispatcher | `DispatchLeaseRenewed` | Requires same owner/token and monotonic expiration. |
| LeaseRenewed | Released | Dispatcher/runtime | `DispatchClaimReleased` | Release is ignored if already completed. |
| Claimed | Expired | Runtime sweeper | `DispatchClaimExpired` | Requires current time beyond expiration plus grace. |
| Expired | Reclaimed | Dispatcher | `DispatchClaimReclaimed` | New token and attempt budget check. |
| Claimed | Completed | Runtime | `DispatchClaimCompleted` | Result idempotency key prevents duplicate application. |
| Claimed | Cancelled | Runtime | `DispatchClaimCancelled` | Requires cancellation request. |

## Dispatcher Contract

Dispatcher flow:

1. Query ready work from runtime queue.
2. Create claim with owner ID, token, lease expiration, and attempt number.
3. Load immutable instance plan and selected strategy binding.
4. Invoke strategy with `StepExecutionContext`.
5. Convert output to a `StrategyResultEnvelope`.
6. Submit result to runtime with claim token and idempotency key.
7. Release or complete claim.

The dispatcher does not decide recovery, branch outcome, or artifact validity directly. It may perform mechanical result validation and classify strategy execution failure.

## Failure Behavior

| Failure | Behavior |
| --- | --- |
| Lease lost during execution | Dispatcher result is rejected; runtime may reclaim if idempotency permits. |
| Duplicate strategy result | Runtime recognizes result idempotency key and returns existing outcome. |
| Strategy throws | Dispatcher returns a strategy fault envelope; manager preprocesses incident. |
| Dispatcher crashes after claim | Lease expires; runtime emits claim expiration and reclaims within attempt budget. |
| Runtime transition conflict | Transition rejected and emitted as restricted diagnostic if it indicates corruption. |
| Cancellation during running step | Runtime marks cancel requested; dispatcher strategy receives cancellation token if supported. |

## Invariants

- Runtime is the only writer of runtime state.
- Dispatcher never mutates runtime state directly.
- Every state transition emits an event in the same transaction or through a reliable outbox.
- Claim ownership is checked on every result submission.
- Terminal run states are immutable except for audit annotations.
- Retry, recovery, and branch loop budgets are consumed through runtime transitions, not ad hoc counters.

## Boundary Rules

- `Processes.Runtime` does not reference Razor or UI modules.
- Runtime may reference persistence abstractions and driver abstractions, but not concrete driver implementations.
- Dispatcher invokes strategy interfaces selected in the plan; it does not search for domain behavior at execution time.
- External execution IDs are metadata, not runtime state authority.
- Runtime does not reference EF, DbContext, migrations, SQL, or provider-specific persistence APIs.
- Runtime writes state/events/artifact-ledger/outbox records through ports described in `architecture/12-runtime-persistence-event-store-and-outbox.md`.
- Idempotency uniqueness for runtime commands, dispatch results, manager decisions, artifact ledger events, and outbox messages is enforced by persistence implementation.

## v3 Detail References

- Persistence ports, event store, outbox, idempotency, crash/retry, and replay: `architecture/12-runtime-persistence-event-store-and-outbox.md`.
- Branch route transitions and loop budgets: `architecture/13-branch-switch-and-loop-contract.md`.
- Manager decisions and recovery control loop: `architecture/14-manager-runtime-and-control-loop.md`.

## Test Implications

- Runtime tests cover each allowed and rejected transition.
- Dispatcher tests cover claim creation, heartbeat, expiration, reclaim, duplicate result, lost lease, crash recovery simulation, cancellation, and strategy fault normalization.
- Integration tests prove runtime event emission and state persistence are atomic from a reader perspective.
- Load/concurrency tests prove many concurrent ready steps do not produce double execution.
