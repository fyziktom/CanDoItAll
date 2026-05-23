# PostgreSQL runtime primitives

This document defines the direction for SB05 and SB06.

## General primitives to prefer

### Work claiming

Use a transaction-safe pattern that claims pending records atomically.

Possible SQL pattern:

```sql
WITH candidate AS (
    SELECT id
    FROM workflow_items
    WHERE status = 'Pending'
      AND (locked_until_utc IS NULL OR locked_until_utc < now())
    ORDER BY priority DESC, created_at_utc
    FOR UPDATE SKIP LOCKED
    LIMIT @batchSize
)
UPDATE workflow_items wi
SET status = 'Claimed',
    locked_by = @workerId,
    locked_until_utc = @lockedUntilUtc
FROM candidate
WHERE wi.id = candidate.id
RETURNING wi.*;
```

Actual table/column names must be adapted to the repository model.

### Idempotency

Every workflow/outbox executor should have durable idempotency boundaries:

- durable operation id,
- attempt counter,
- status,
- last error,
- lease/lock expiration,
- completed timestamp,
- deterministic retry decision.

### Retry behavior

Use PostgreSQL/Npgsql transient retry handling where appropriate, but do not retry non-idempotent external side effects without a durable idempotency key.

## Do not do in SB05

- Do not deeply refactor process semantics.
- Do not rewrite workflows before general runtime limitations are cleaned up.
- Do not introduce PostgreSQL-specific raw SQL everywhere without centralizing patterns.

## Do in SB06

- Apply the general primitives to processes/workflows/automation/outbox.
- Add concurrency tests.
- Add negative double-claim tests.
