# Runtime Persistence Event Store And Outbox

## Design Intent

Runtime owns state transitions and emits events. Persistence owns storage implementation. The boundary must be concrete enough that future implementation cannot rebuild query-first observation or put EF details into runtime.

The recommended transaction model is a single durable persistence transaction for each accepted runtime command: update runtime state, append runtime events, append artifact ledger events when applicable, and enqueue outbox messages before commit.

## Persistence Ports

Runtime-facing ports:

```csharp
public interface IProcessRuntimeUnitOfWork
{
    Task<ProcessRuntimeCommitResult> CommitAsync(
        ProcessRuntimeCommitRequest request,
        CancellationToken cancellationToken);
}
```

Port categories:

- `IProcessRuntimeStateStore`: load/update process, step, claim, budget, incident, and manager state.
- `IProcessRuntimeEventStore`: append and read `ProcessRuntimeEventEnvelope`.
- `IProcessArtifactLedgerStore`: append and read artifact ledger events, validation records, lineage, and references.
- `IProcessOutboxWriter`: enqueue projector/observer/external notifications.
- `IProcessProjectionStore`: read/write current snapshots, historical projections, offsets, and dead letters.
- `IProcessIdempotencyStore`: reserve and resolve command/result idempotency keys.

`Processes.Persistence` implements these ports using EF/PostgreSQL. Runtime sees ports only.

## Transaction Model

One accepted runtime command commits:

1. idempotency key reservation or duplicate lookup,
2. current state load with concurrency token,
3. transition validation,
4. state mutation record,
5. runtime event append,
6. artifact ledger append when artifact state changes,
7. outbox message enqueue,
8. idempotency key completion,
9. transaction commit.

If commit fails before the database commit, the command can be retried with the same idempotency key. If commit succeeds but dispatcher does not receive the response, retry returns the previous commit result.

## Event Sequence Model

Use both:

- global monotonically increasing store sequence for projector ordering,
- per-root-run sequence for run-local replay and debugging.

Every event contains:

- event ID,
- global sequence,
- root run ID,
- run ID,
- optional step ID,
- event type,
- occurred-at UTC,
- actor,
- correlation ID,
- causation ID,
- payload schema version,
- payload JSON,
- sensitivity.

## Idempotency Unique Keys

Required unique keys:

- runtime command key: `(RunId, CommandId)`
- dispatch claim key: `(StepInstanceId, ClaimToken)`
- strategy result key: `(StepInstanceId, StrategyId, ResultIdempotencyKey)`
- manager decision key: `(RunId, IncidentId, DecisionIdempotencyKey)`
- artifact ledger key: `(ArtifactSlotId, LedgerEventId)`
- outbox message key: `(EventId, SubscriberKind)`
- projection offset key: `(ProjectorName, ShardKey)`

## Schema Versions And Upcasters

- Events are immutable.
- Payload schema version is required.
- Upcasters convert old event payloads into current projection input models.
- Runtime transition code reads current runtime state, not arbitrary old event payloads, except during replay tools.
- Upcasters live outside core and are tested with golden payload fixtures.

## Projector Offsets And Dead Letters

Projection workers store offsets per projector and shard. A projector processes events in global sequence order for its shard.

Failure policy:

- transient failure retries with bounded backoff,
- deterministic payload failure writes dead letter with event ID, projector name, error class, restricted diagnostic reference, and retry policy,
- poison events do not block unrelated projectors,
- replay can start from offset, time range, run ID, or full rebuild.

## Crash And Retry Behavior

| Crash point | Behavior |
| --- | --- |
| Before transaction commit | Retry command; no durable state change exists. |
| After state/event/outbox commit before response | Retry idempotency key returns committed result. |
| Dispatcher crashes with active claim | Lease expiry emits claim-expired event and work can be reclaimed. |
| Projector crashes before offset commit | Event is reprocessed; projectors must be idempotent. |
| Projector crashes after projection write before offset | Projection write must be idempotent by event sequence/version. |

## EF/PostgreSQL Boundary

Persistence may define:

- EF entities,
- DbContext mappings,
- migrations,
- concurrency tokens,
- unique indexes,
- SQL-specific optimized queries,
- transactional outbox implementation.

Runtime may not reference any of those. Runtime tests can use in-memory/fake port implementations; persistence tests prove EF/PostgreSQL behavior.

## Invariants

- Runtime state and event append commit atomically from a reader perspective.
- Events are append-only.
- Outbox records are transactionally tied to event append.
- Projection state is derived and rebuildable.
- Idempotency keys are mandatory for dispatcher results, manager decisions, recovery attempts, and external callbacks.
- Raw diagnostics stored through persistence maintain sensitivity classification.

## Failure Behavior

| Failure | Required response |
| --- | --- |
| Duplicate command key | Return original result. |
| Concurrency conflict | Reload state and retry transition only when command is idempotent and still valid. |
| Unknown event schema | Dead-letter projector event; do not drop event. |
| Outbox delivery failure | Retry outbox independently after commit. |
| Projection lag | Expose lag/freshness metadata to UI and operators. |

## Test Implications

- Runtime port tests cover idempotency, duplicate result, concurrency conflict, and atomic event/outbox writes.
- Persistence tests cover unique indexes, transaction rollback, outbox retry, event ordering, artifact ledger append, projection offset, dead-letter, and replay.
- Projector tests cover idempotent writes and upcaster behavior.
