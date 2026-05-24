# Conditional finalization pattern

Example PostgreSQL-safe finalization pattern:

```sql
UPDATE "Processes_Outbox"
SET
    "Status" = @newStatus,
    "CompletedAtUtc" = @completedAtUtc,
    "NextAttemptAtUtc" = @nextAttemptAtUtc,
    "LastError" = @lastError,
    "LeaseToken" = '',
    "LeaseExpiresAtUtc" = NULL,
    "UpdatedAtUtc" = @now
WHERE "Id" = @id
  AND "LeaseToken" = @leaseToken
  AND "LeaseExpiresAtUtc" > @now
RETURNING "Id";
```

If zero rows are returned:
- do not insert non-idempotent completion audit,
- do not report success,
- log `lease-lost`,
- allow another worker to retry based on the current row state.

For audit rows:
- either insert audit in the same transaction after finalization succeeds,
- or use deterministic idempotency keys such as `connector-command:{id}:attempt:{attemptNumber}:completed`.
