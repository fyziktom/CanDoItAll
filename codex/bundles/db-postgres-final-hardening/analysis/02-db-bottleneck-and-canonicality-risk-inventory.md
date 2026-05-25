# DB bottleneck and canonicality risk inventory

## B1: Tracked-entity finalization after leased work

Affected:
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`
- Review also: `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`

Risk:
A worker claims a row, loads a tracked entity, performs external work, and saves final status. If lease renewal failed or the lease was reclaimed, final `SaveChangesAsync` may still commit stale state.

Required fix:
Finalization must be conditional:
- update only when `Id`, `LeaseToken`, and non-expired `LeaseExpiresAtUtc` still match,
- insert audit/attempt records only as part of the same guarded finalization transaction or with idempotent guard,
- return a lease-lost result if the final conditional update affects zero rows.

## B2: Heartbeat renewal loss is not a hard stop

Affected:
- `ProcessOutboxService.RenewClaimedLeaseAsync`
- `RenewLeaseUntilDispatchCompletesAsync`
- any equivalent connector/automation long-work heartbeat if introduced.

Risk:
A warning-only heartbeat failure can allow long work to continue and attempt finalization.

Required fix:
Convert lease renewal failure into an observable lease-loss signal. The worker may allow external work to finish for cleanup, but it must not commit canonical DB state.

## B3: Parallelism exists but may not be enabled or measured

Affected:
- automation message dispatch parallelism
- process outbox batch parallelism
- connector outbox batch parallelism
- appsettings defaults and options validation

Required fix:
Define defaults, maximums, and benchmark proof:
- conservative but >1 defaults for PostgreSQL,
- explicit opt-out for single-threaded behavior,
- per-partition serialization where canonicality requires it,
- benchmark under synthetic but realistic load.

## B4: Process candidate claim-first flow still needs query-count proof

Affected:
- `LoadDispatchCandidateHeadersAsync`
- `LoadDispatchCandidateAsync(processRunId, stepRunId, ...)`

Required fix:
Prove the pre-claim path only loads minimal candidate headers and does not hydrate full process state before claim. Add telemetry/assertions for query count or at least source-level audit plus integration proof.

## B5: Profile-specific context factory boundary

Affected:
- `IProfileAppDbContextFactory`
- database transfer/schema/bootstrap flows

Required fix:
Make it hard to accidentally inject profile-specific factory into normal runtime workers. Consider naming, analyzer/test, or grep-based architectural test that allows it only in approved maintenance classes.

## B6: Validation caveats

Affected:
- broad integration suite
- broad component suite
- EF Core warning inventory

Required fix:
Close broad-suite validation or produce explicit quarantine list with owner and reason. Do not rely only on focused tests for merge.
