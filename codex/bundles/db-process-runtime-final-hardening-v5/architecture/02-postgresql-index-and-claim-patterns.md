# PostgreSQL index and claim patterns

## Claim pattern

Use a single atomic claim operation when possible:

```sql
WITH due AS (
  SELECT "Id"
  FROM "<table>"
  WHERE ...
  ORDER BY ...
  FOR UPDATE SKIP LOCKED
  LIMIT @take
)
UPDATE "<table>" AS t
SET ...
FROM due
WHERE t."Id" = due."Id"
RETURNING ...;
```

## Required query-plan proof

For each hot claim query:

- seed at least thousands of rows,
- run `EXPLAIN (ANALYZE, BUFFERS)`,
- prove index scan or acceptable plan,
- save plan under `proof/SB05/query-plans/`.

## Candidate indexes

Process outbox:

- status + next attempt + lease expiry + created time
- process run + command key + created time
- optional partial index for pending rows

Process step dispatch:

- process run + status + sequence
- process run + automation dispatch lease expiry
- optional partial index for dispatchable statuses

Automation delivery:

- state + available time + locked time + created time
- envelope id for grouped processing

Connector outbox:

- status + approval state + next attempt + lease expiry + created time
- plugin/project/command partition fields
