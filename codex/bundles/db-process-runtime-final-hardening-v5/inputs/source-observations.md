# Source observations from repository review

## Strong progress already visible

- `src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs` no longer contains context leasing/drain/hot-switch machinery; it now exposes runtime snapshot and restart-observed notification only.
- `src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs` has been replaced with `IProfileAppDbContextFactory` / `ProfileAppDbContextFactory`, making profile-specific contexts a special path rather than normal runtime path.
- `src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` registers `AddPooledDbContextFactory<AppDbContext>` using `ICanonicalRuntimeDatabase`.
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` now uses `FOR UPDATE SKIP LOCKED`, conditional attempt start, lease renewal monitor, and conditional finalization.
- `src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs` now rejects lease-less canonical processing, conditionally starts attempts, and conditionally finalizes.
- `src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs` now uses conditional delivery finalization inside a transaction.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` now loads candidate headers, claims a step, hydrates the candidate afterward, and uses claim-aware mutation paths.

## Remaining high-risk observations

### Startup recovery still clears active non-expired leases

`src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs` has `ReleaseStrandedAutomationDispatchLeasesAsync`, which loads pending automation dispatch outbox records, then clears `LeaseToken` and `LeaseExpiresAtUtc` when `LeaseExpiresAtUtc > now`.

That is unsafe because a non-expired lease is not proven stranded. In a multi-process/multi-node deployment, or during slow startup, this can break an active worker's canonical ownership.

### Process dispatch step claim has no continuous heartbeat while AgentFramework execution is blocked

`ProcessRunAutomationDispatchService.Execution.cs` calls `renewLeaseAsync` before an attempt and again at some transition points, but `workspaceService.ExecuteRunAsync(...)` can be a long blocking operation. If it runs longer than `StepDispatchClaimLeaseDuration`, the step claim can expire while the original worker is still running. The final mutation guard prevents stale finalization, but duplicate long work may still be created.

### Process outbox side effects are still at-least-once

`ProcessOutbox.DispatchAsync` can perform search upserts, search deletes, activity writes, and automation dispatch. Conditional finalization prevents stale canonical updates, but if a lease is lost after an external side effect is performed, retry may repeat the side effect. The system needs explicit idempotency proof and, where missing, stable idempotency keys.

### Query/index proof is missing

Source code contains PostgreSQL claim patterns, but the proof so far has no `EXPLAIN (ANALYZE, BUFFERS)` or database index proof for the hot claim queries.

### Numeric benchmark is missing

The benchmark report explicitly says no numeric before/after wall-clock benchmark was captured. Deterministic source proof is useful but not enough to prove that the bottleneck is actually removed under load.

### Broad validation still has caveats

The final report says broad integration suite timed out and hit PostgreSQL auth failures; broad component suite also timed out earlier. This must be closed or explicitly quarantined before merging.
