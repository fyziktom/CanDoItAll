# Source observations from branch review

## GitHub compare

`db-remove-sqlite` is now ahead of `development` and behind by `0` in the GitHub compare result. This fixes the previous divergence problem.

## Confirmed improvements

- `DatabaseProviderKind` now contains only `PostgreSql` and `InMemory`.
- `DatabaseProfileSourceKind` now contains only `PostgresConnection` and `InMemory`.
- `DatabaseProfileStorageMode.ManagedPerProfile` and `DatabaseProfileResolutionSource.LegacyDiscovery` were removed.
- `IDatabaseSnapshotService` and snapshot runtime models were removed from the main runtime surface.
- `SwitchableAppDbContextFactory` no longer resolves the current profile for every normal context; it delegates normal `CreateDbContext*` to the canonical pooled `IDbContextFactory<AppDbContext>`.
- `AddPooledDbContextFactory<AppDbContext>` is registered and configured from `ICanonicalRuntimeDatabase`.
- `DatabaseSwitchCoordinator` no longer hot-switches the running process; it persists activation and returns `RequiresRestart = true`.
- Batch PostgreSQL claim SQL using `FOR UPDATE SKIP LOCKED` was introduced in automation delivery, process outbox, and connector outbox paths.
- Process step runs now have `AutomationDispatchClaimToken`, `AutomationDispatchClaimedBy`, `AutomationDispatchClaimedAtUtc`, `AutomationDispatchLeaseExpiresAtUtc`, and `AutomationDispatchAttemptCount`.

## Open problems

- `DatabaseRuntimeSwitching.cs` still contains switch locks, context leases, drain signals, and switch sessions. These appear to be dead or misleading for normal runtime.
- `DatabaseOptions.EnableMaintenanceHotSwitch` exists but the switch coordinator always returns `RequiresRestart = true`; the option appears misleading.
- Claimed automation deliveries, process outbox records, and connector commands are still processed sequentially after batch claim.
- `ProcessRunAutomationDispatchService.DispatchAsync` still uses process-local `StepDispatchGuards` and heavy candidate loading before durable claim.
- Dispatch claim renewal only logs warning if renewal fails. Stale long-running executions must not be allowed to commit transitions after losing their durable claim.
- UI/API must clearly distinguish runtime canonical DB from pending activation for next restart.
- Broad validation was still reported as environment-limited in Codex's execution report. It needs rerun or precise closure.
