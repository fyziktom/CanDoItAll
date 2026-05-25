# Semantic invariants SB07

## Invariants to prove

- No stale worker may write canonical process DB state.
- Lease ownership must be explicit and verifiable.
- Retry behavior must be idempotent.
- PostgreSQL runtime must remain canonical.
- Dispatch candidate hydration must not happen before a durable claim.
- Pending profile activation must not change current runtime paths before restart.

## Negative proof

- A worker holding an old process outbox lease is forced to lose ownership before finalization.
- Recovery is tested against both live and expired leases so an implementation that blindly clears leases fails one side of the pair.
- Heartbeat renewal is tested against both successful repeated renewal and renewal failure.
- Dispatch source-order tests fail if full candidate hydration moves ahead of durable claim, or if artifact projection moves ahead of claim-loss checking.
- Database switching seeds different records into current and pending profiles to prove current runtime context does not drift before restart.

## Positive proof

- `bundle://proof/SB07/red-team-tests.log` passed 11 focused red-team tests.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs` proves same-record lease contention, stale finalization suppression, recovery live/expired lease handling, and idempotent outbox retries.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` proves dispatch heartbeat renewal and cancellation on renewal failure.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDatabaseRedTeamSourceInvariantTests.cs` proves claim-before-hydration and guard-before-projection ordering.
- `repo://tests/CanDoItAll.Tests.Integration/DatabaseRuntimeSwitchingIntegrationTests.cs` proves pending restart profile activation is isolated from active runtime DbContext paths.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Process outbox lease ownership | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs` | `bundle://proof/SB07/red-team-tests.log` | stale worker cannot finalize after lease steal |
| Recovery live/expired lease split | `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs` | `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs` | `bundle://proof/SB07/red-team-tests.log` | live lease preserved, expired lease reclaimed |
| Dispatch heartbeat | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs` | `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | `bundle://proof/SB07/red-team-tests.log` | renewal failure cancels dispatch |
| Dispatch source-order guard | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `repo://tests/CanDoItAll.Tests.Integration/ProcessDatabaseRedTeamSourceInvariantTests.cs` | `bundle://proof/SB07/red-team-tests.log` | invariant fails if hydration/projection order regresses |
| Runtime profile isolation | `repo://src/CanDoItAll.Infrastructure/ControlPlane` | `repo://tests/CanDoItAll.Tests.Integration/DatabaseRuntimeSwitchingIntegrationTests.cs` | `bundle://proof/SB07/red-team-tests.log` | alpha/beta seeded rows prove pending switch is restart-only |
