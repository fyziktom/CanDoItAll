# Target process DB architecture

## Runtime DB

- PostgreSQL is the only persistent runtime database.
- The application resolves one canonical runtime DB profile at startup.
- Normal runtime services use pooled `IDbContextFactory<AppDbContext>`.
- Profile-specific contexts are named and treated as maintenance-only.

## Process outbox

Process outbox owns durable side effects. It should be at-least-once and idempotent.

Recommended model additions:

- `LeaseOwnerInstanceId` on `ProcessOutboxRecord`
- `LeaseAcquiredAtUtc` on `ProcessOutboxRecord`
- optional `IdempotencyKey` or `DedupeKey` on `ProcessOutboxRecord`
- unique index for automation dispatch dedupe where applicable

## Process dispatch

Process dispatch owns step automation execution and artifact/state transition projection.

Recommended model additions or proof:

- durable claim owner instance already exists on step runs as `AutomationDispatchClaimedBy`
- heartbeat must renew `AutomationDispatchLeaseExpiresAtUtc` continuously during long work
- all mutation paths must call claim verification immediately before mutation

## Recovery

Recovery is not a force-unlock mechanism. Recovery should:

- enqueue missing work only when no pending dispatch exists,
- reclaim only expired leases,
- optionally reclaim owned leases when the owner runtime instance is proven dead,
- record recovery ledger events.
