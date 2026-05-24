# PostgreSQL concurrency patterns

## Batch claim

Preferred pattern:

```sql
WITH due AS (
    SELECT "Id"
    FROM ...
    WHERE ...
    ORDER BY ...
    FOR UPDATE SKIP LOCKED
    LIMIT @take
)
UPDATE ...
SET "LeaseToken" = ..., "LeaseExpiresAtUtc" = ...
FROM due
WHERE ... "Id" = due."Id"
RETURNING ...;
```

## Bounded parallel execution

After batch claim:
- run claimed items through `Parallel.ForEachAsync` or a bounded task scheduler;
- each item uses a fresh `DbContext`;
- each item verifies its lease token before state transitions;
- partition by aggregate key where necessary.

## Partition rules

- Automation deliveries: partition by `EnvelopeId` unless aggregate update is proven atomic.
- Process outbox: partition by `ProcessRunId` and command class when command order matters.
- Connector outbox: partition by connector account/plugin/external system where rate limits or order matter.
- Process step dispatch: one active claim per `ProcessStepRun.Id`.

## Retry and stale lease

Expired leases may be reclaimed, but stale workers must not commit after losing ownership.
