# Branch review

## Fulfilled

- PostgreSQL-only main runtime is structurally implemented.
- SQLite typed runtime profile model has been removed.
- Runtime DB hot path uses canonical pooled `AppDbContext` factory.
- Legacy hot-switch context drain has been removed from the normal runtime state.
- Profile-specific context factory is separated.
- PostgreSQL batch claim exists for automation, connector, and process outboxes.
- Process dispatch has moved toward claim-first candidate loading.
- Conditional finalization now exists for process outbox, connector outbox, and automation delivery.

## Not fully closed

- Startup recovery can still clear live non-expired process automation dispatch leases.
- Process step dispatch claim does not have a continuous heartbeat around long blocking agent execution.
- Process side effects are not yet proven idempotent under lease loss and retry.
- Claim queries are not backed by query-plan/index proof.
- Numeric throughput benchmark is still missing.
- Broad test-suite caveats are still open.
- The branch contains many bundle/proof artifacts. Decide whether these are intended to be tracked.
