# Proof manifest SB02

## Status

Completed.

## Owned requirements

- R3: Process startup recovery must not steal live leases.
- R9: Process DB tests must red-team canonicality.

## Semantic invariant contract

`bundle://proof/SB02/semantic-invariants.md`

## Changed files

| File | Reason |
|---|---|
| `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs` | Replaced startup non-expired lease release with expired-only lease release. |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs` | Added recovery tests for live non-expired and expired automation dispatch leases. |

## Validation commands

| Command | Result | Transcript |
|---|---|---|
| Focused recovery lease tests before production fix | Failed as expected | `bundle://proof/SB02/recovery-lease-tests-failing-first.log` |
| Focused recovery lease tests after production fix | Passed, 2 tests | `bundle://proof/SB02/recovery-lease-tests.log` |
| Recovery source audit | Passed | `bundle://proof/SB02/recovery-source-audit.log` |

## Source assertions

- `ReleaseExpiredAutomationDispatchLeasesAsync` only clears rows where `LeaseExpiresAtUtc.Value <= now`.
- Startup recovery log text says `expired`, not `stranded`.
- `HasPendingAutomationDispatchAsync` still prevents duplicate recovery enqueue while a pending automation dispatch record exists.

## Positive proof

`RecoverActiveRunsAsync_releases_expired_startup_automation_dispatch_leases` proves an expired pending dispatch lease is cleared and then processed once by the outbox worker.

## Negative proof

`RecoverActiveRunsAsync_preserves_live_startup_automation_dispatch_leases` proves two startup recovery scans do not clear a non-expired lease and do not let another worker process it.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProcessOutboxRecord.LeaseToken` / `LeaseExpiresAtUtc` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` claims automation dispatch records | `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs` clears only expired leases | `bundle://proof/SB02/recovery-lease-tests.log` | `bundle://proof/SB02/recovery-lease-tests-failing-first.log` and `bundle://proof/SB02/recovery-lease-tests.log` |

## Semantic adequacy

The shallow-pass trap was to clear any startup lease and rely on quick retry. The failing-first transcript shows that behavior violates live lease ownership. The passing transcript verifies both the live and expired branches against PostgreSQL-backed integration tests.

## Residual risks

This phase does not add runtime-instance owner-death takeover. Non-expired leases are left alone until the owner renews or the lease expires naturally.
