# Process DB risk analysis

## Critical process DB risk 1: non-expired lease reclaim

The recovery worker currently treats pending automation dispatch leases as reclaimable during startup even when `LeaseExpiresAtUtc > now`.

This is unsafe unless the system can prove the lease owner is a dead previous runtime instance. The current `ProcessOutboxRecord` model does not contain a lease owner instance id. Therefore, the safest immediate fix is:

- never clear non-expired process outbox leases during startup recovery,
- let active workers renew or let leases expire naturally,
- add owner metadata if active takeover is later required.

## Critical process DB risk 2: long AgentFramework execution and process step claim expiry

The process step dispatch claim is renewed only through explicit callback calls. A single long-running `workspaceService.ExecuteRunAsync` may block for longer than the claim lease duration.

This should be fixed by a scoped claim heartbeat around long dispatch sections:

- start a heartbeat task after durable step claim is acquired,
- renew both process step dispatch claim and outer outbox lease,
- stop heartbeat only after all canonical mutation work is done or dispatch is aborted,
- treat heartbeat failure as claim lost and suppress canonical mutation.

## Critical process DB risk 3: at-least-once side effects

The process outbox intentionally provides durable retry, so side effects must be idempotent. Current conditional finalization prevents stale state writes, but external side effects can still happen before finalization fails.

Every process outbox side effect must have stable idempotency semantics:

- search upsert is idempotent by source key,
- search delete is idempotent by source key,
- activity write must have stable idempotency key,
- automation dispatch enqueue should dedupe by stable run/step/trigger/outbox command identity,
- any future side effect must include an idempotency key or be marked non-retryable with compensation logic.

## Critical process DB risk 4: query/index proof

Claim queries are now PostgreSQL-specific. They should be supported by PostgreSQL indexes and proven by `EXPLAIN (ANALYZE, BUFFERS)` on seeded data.

## Critical process DB risk 5: benchmarks

Source proof shows parallelism exists, but the user specifically wants bottlenecks removed. This needs numeric proof, not just code inspection.
