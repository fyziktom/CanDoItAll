# PostgreSQL concurrency patterns

## Batch claim template

Use one SQL roundtrip to claim a batch:

```sql
WITH candidate AS (
    SELECT "Id"
    FROM "Automation_EnvelopeDeliveries"
    WHERE "AvailableAtUtc" <= @now
      AND (
          "State" IN (@pending, @retry)
          OR ("State" = @running AND "LockedAtUtc" <= @leaseCutoff)
      )
    ORDER BY "AvailableAtUtc", "CreatedAtUtc"
    FOR UPDATE SKIP LOCKED
    LIMIT @take
)
UPDATE "Automation_EnvelopeDeliveries" d
SET "State" = @running,
    "AttemptCount" = d."AttemptCount" + 1,
    "LastAttemptAtUtc" = @now,
    "UpdatedAtUtc" = @now,
    "CompletedAtUtc" = NULL,
    "LockedAtUtc" = @now,
    "LockToken" = @lockToken
FROM candidate
WHERE d."Id" = candidate."Id"
RETURNING d."Id", d."EnvelopeId", d."HandlerKey", d."LockToken", d."AttemptCount";
```

## Process step claim template

Add or reuse durable state on step runs:

```text
AutomationClaimToken
AutomationClaimedAtUtc
AutomationClaimedBy
AutomationLeaseExpiresAtUtc
CurrentAutomationExecutionRunId
AutomationAttemptCount
```

Claim should be atomic:

```sql
UPDATE "Process_StepRuns"
SET ...
WHERE "Id" = @stepRunId
  AND "Status" IN (...)
  AND ("AutomationLeaseExpiresAtUtc" IS NULL OR "AutomationLeaseExpiresAtUtc" <= @now)
  AND "ConcurrencyToken" = @expectedToken
RETURNING ...
```

## Required negative tests

- N workers race for one due delivery: exactly one claim.
- N workers race for one step run: exactly one canonical execution.
- Stale lease is reclaimed only after timeout.
- Non-stale lease is not reclaimed.
- Completion transition with stale token is rejected.
- Two different process steps can execute concurrently.
- Same process step is not serialized by static in-memory lock only.
