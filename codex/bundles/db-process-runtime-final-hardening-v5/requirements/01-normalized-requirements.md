# Normalized requirements

## R1 - Keep PostgreSQL-only runtime canonical

Normal runtime DB access must use one canonical runtime database profile resolved at startup.

## R2 - No SQLite runtime provider

No SQLite provider, driver, migration project, runtime profile kind, or UI source may exist in runtime source/tests except explicit legacy quarantine/documentation/bundle artifacts.

## R3 - Process startup recovery must not steal live leases

Process recovery must not release non-expired outbox or step dispatch leases unless it can prove the lease owner is a dead runtime instance.

## R4 - Long-running process dispatch must keep claims alive

Process automation dispatch must continuously renew durable step claim and outer outbox lease while long-running agent/workflow execution is in flight.

## R5 - Process side effects must be idempotent

Every process outbox side effect must have an observable idempotency contract and a negative duplicate/retry test.

## R6 - PostgreSQL claim queries must be indexed and measured

Claim queries for process outbox, connector outbox, automation delivery, and process dispatch steps must have index/query-plan proof.

## R7 - Throughput improvement must be measurable

Provide numeric benchmark output for at least one seeded PostgreSQL workload showing throughput before/after or sequential vs bounded parallel processing.

## R8 - Broad validation caveats must be closed

Full component/integration suite caveats must be resolved, or explicitly quarantined with owner, reason, and non-impact proof.

## R9 - Process DB tests must red-team canonicality

Add tests for duplicate workers, lease loss, lease expiry, recovery scan, and stale worker finalization suppression.
