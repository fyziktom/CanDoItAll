# Target architecture

## Runtime database

- `ICanonicalRuntimeDatabase` is the only runtime source of truth.
- `IDbContextFactory<AppDbContext>` is configured from the canonical profile and may be pooled.
- Profile-specific context creation is maintenance-only and must use `IProfileAppDbContextFactory`.

## Pending activation

- `IDatabaseSwitchCoordinator.SwitchAsync` persists pending activation and returns `RequiresRestart = true`.
- It must not mutate the running canonical factory/profile.
- UI/API must display both runtime profile and pending restart profile when they differ.

## Leased work

Claiming and finalization must be separate but both protected:
1. claim: `FOR UPDATE SKIP LOCKED` / conditional update returns claimed IDs and tokens,
2. work: bounded parallel execution partitioned by canonical key,
3. finalization: conditional `UPDATE ... WHERE Id = @id AND LeaseToken = @token AND LeaseExpiresAtUtc > @now`,
4. audit: attempt/audit rows must be idempotent or transactionally tied to finalization.

## Throughput

Use bounded parallelism:
- Automation deliveries: partition by envelope id or handler-specific ordering key.
- Process outbox: partition by process run id + command key unless command explicitly allows wider parallelism.
- Connector commands: partition by connector plugin + target account/resource key, not only command type if possible.

## Validation

Proof must include:
- source audit,
- focused tests,
- broad tests or explicit quarantine,
- numeric benchmark.
