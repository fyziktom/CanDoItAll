# Source observations from current branch review

## Fulfilled

- Branch `db-remove-sqlite` is now ahead of `development` and behind by 0.
- `DatabaseRuntimeSwitching.cs` no longer contains active context drain, context lease acquisition, switch sessions, `_contextsAllowed`, or `_drainSignal`. Runtime state is now snapshot-like.
- `SwitchableAppDbContextFactory` was renamed/converted to `IProfileAppDbContextFactory` + `ProfileAppDbContextFactory`. Normal runtime uses `IDbContextFactory<AppDbContext>` from `AddPooledDbContextFactory`.
- `CanonicalRuntimeDatabase` initializes a canonical startup profile and runtime profile accessor returns it.
- `DatabaseSwitchResult` includes runtime vs pending restart profile signals.
- `DatabaseProfileSummary` and `DatabaseSelectionStateModel` include pending restart activation fields.
- Data Sources/workspace services use canonical runtime accessor for current state and still use profile-specific factory for schema/transfer/maintenance operations.
- PostgreSQL `FOR UPDATE SKIP LOCKED` claim patterns exist in automation deliveries, process outbox, and connector outbox.
- Process dispatch now loads candidate headers first, claims a step, then hydrates a candidate, and checks claim before transitions/artifact projection.
- Codex recorded final build/unit/focused test evidence.

## Remaining gaps / risks

- Broad integration suite timed out and hit PostgreSQL auth failures. Broad component suite also timed out earlier. The report relies on focused tests.
- No numeric performance benchmark was captured; throughput proof is mostly source/audit based.
- `ProcessOutboxService.ProcessClaimedAsync` loads a claimed row into a tracked entity, renews lease in a background task, performs external work, and then mutates/saves the same entity. If the lease is lost or expired, final save may still commit stale completion/retry/dead-letter state unless a final conditional update verifies lease ownership.
- `ConnectorCommandProcessor.ProcessAsync` follows the same tracked entity pattern after executing a plugin handler. It validates the lease before starting, but finalization is not visibly conditional on still owning the lease.
- `ProcessOutboxService.RenewClaimedLeaseAsync` logs when renewal updates 0 rows, but `RenewLeaseUntilDispatchCompletesAsync` continues and retries instead of causing the dispatch to stop or preventing finalization.
- `AutomationMessageDispatcher` also uses a claimed delivery model; review whether handler execution is short enough or whether finalization should be CAS-guarded similarly.
- Bounded parallelism exists, but it needs verified defaults, configuration documentation, safe upper bounds, and a benchmark that shows throughput improved without duplicate execution.
- `StepDispatchGuards` still exists as local fast-path guard. It is acceptable only if short-lived and if durable claim remains authoritative.
- Profile-specific context factory is valid for maintenance/transfer/schema checks, but runtime code must not accidentally use it for normal request/worker hot path.
