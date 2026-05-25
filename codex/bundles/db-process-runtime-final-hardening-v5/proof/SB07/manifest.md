# Proof manifest SB07

## Status

Completed.

## Owned requirements

- R1: Stale process outbox workers must not finalize canonical state.
- R2: Startup recovery must preserve live automation dispatch leases and reclaim only expired leases.
- R3: Long-running process dispatch must maintain or lose a durable heartbeat explicitly.
- R7: Red-team process DB tests must prove canonicality under adversarial concurrency.
- R8: Pending database profile activation must not change canonical runtime paths before restart.

## Changed files

| File | SHA-256 | Reason |
|---|---:|---|
| `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs` | `EDA0757FF6149E7BD6B316298048DA2DA574540B2C6802622776B5A7F2F625EE` | PostgreSQL-backed red-team coverage for process outbox lease contention, stale finalization, recovery lease preservation/reclaim, and idempotent side effects. |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `018AECFA5C3C0C6BBF9307DC8142BB4E266E37BE738812063EE976FCCB2B2CA4` | Red-team coverage for dispatch heartbeat renewal and cancellation on renewal failure. |
| `repo://tests/CanDoItAll.Tests.Integration/DatabaseRuntimeSwitchingIntegrationTests.cs` | `D6ADD373D0641FF78BBD06D2EB2E2B837E813D6D4485EF328A2F4F5394ED063F` | Existing PostgreSQL-backed proof that pending activation waits for restart before changing runtime DbContext paths. |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessDatabaseRedTeamSourceInvariantTests.cs` | `4B8850C1BFE7D68243D0D5758A6F1006898907927A9ACE700C4016A666861D54` | Adds source-order invariant tests for claim-before-hydration and claim-loss guard-before-projection/transition. |
| `bundle://proof/SB07/red-team-tests.log` | `65669B0701172913DD8E85D752567CC653E63485689B899955AF240C1349F45B` | Focused red-team test transcript. |
| `bundle://proof/SB07/red-team-source-audit.log` | `B95543C882EFDAB53F058C6759AC3D9749B2B506CF1CE78BD774F734AB0EF935` | Source audit mapping SB07 requirements to tests. |

## Validation commands

| Command | Result | Transcript |
|---|---|---|
| Focused SB07 integration/source red-team suite | Passed, 11 tests | `bundle://proof/SB07/red-team-tests.log` |
| Red-team source audit with `rg` | Passed | `bundle://proof/SB07/red-team-source-audit.log` |

## Source assertions

- `Automation_dispatch_lease_prevents_parallel_reclaim_during_long_agent_work` proves a concurrent process outbox worker cannot reclaim a live automation dispatch lease and only one worker completes the record.
- `Automation_dispatch_stale_worker_cannot_finalize_after_lease_is_stolen` proves a worker that loses its lease after the side effect cannot finalize and the later retry remains idempotent.
- `RecoverActiveRunsAsync_preserves_live_startup_automation_dispatch_leases` and `RecoverActiveRunsAsync_releases_expired_startup_automation_dispatch_leases` prove recovery distinguishes live from expired leases.
- `ProcessDispatchLeaseHeartbeat_*` tests prove continuous renewal and explicit cancellation on renewal failure.
- `ProcessDatabaseRedTeamSourceInvariantTests` proves full candidate hydration occurs only after durable step claim and stale dispatch workers are stopped before artifact projection/completion transition.
- `SwitchAsync_saves_activation_for_next_start_without_changing_running_context` proves pending database profile activation does not affect canonical runtime DbContext paths before restart.

## Semantic adequacy

The focused suite covers all SB07 required tests:

1. same outbox record cannot be finalized by two workers
2. stale worker finalization is suppressed after lease loss
3. live startup automation dispatch leases are preserved
4. expired startup automation dispatch leases are reclaimed safely
5. long-running execution is heartbeat-renewed
6. stale dispatch worker cannot project artifacts or transition after claim loss
7. candidate hydration happens after durable claim
8. pending profile activation does not change runtime workspace paths before restart

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Process outbox lease finalization | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs` | `bundle://proof/SB07/red-team-tests.log` | stale-worker test steals the lease before first worker finalizes |
| Startup recovery lease reclaim | `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs` | `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs` | `bundle://proof/SB07/red-team-tests.log` | live-vs-expired tests assert opposite outcomes |
| Dispatch heartbeat and claim loss | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs` | `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `bundle://proof/SB07/red-team-tests.log` | renewal-failure test forces claim loss |
| Dispatch claim-before-hydration | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `repo://tests/CanDoItAll.Tests.Integration/ProcessDatabaseRedTeamSourceInvariantTests.cs` | `bundle://proof/SB07/red-team-tests.log` | source invariant fails if hydration moves before claim |
| Pending restart runtime profile isolation | `repo://src/CanDoItAll.Infrastructure/ControlPlane` | `repo://tests/CanDoItAll.Tests.Integration/DatabaseRuntimeSwitchingIntegrationTests.cs` | `bundle://proof/SB07/red-team-tests.log` | test seeds alpha and beta profiles and proves active context stays alpha until restart |

## Residual risks

No SB07 test was quarantined. The source-order invariant tests are intentionally narrow guardrails; PostgreSQL-backed behavior is still proven by the process outbox, recovery, heartbeat, and profile-switch integration tests.
