# Semantic invariants SB02

## SB02-I1: non-expired leases are canonical ownership

- Source raw note: startup recovery must not release non-expired process automation dispatch leases unless owner death is proven.
- Expected behavior: `RecoverActiveRunsAsync(... true)` leaves pending automation dispatch rows untouched when `LeaseExpiresAtUtc > now`.
- Disallowed shallow implementation: clear live leases during the first startup recovery scan.
- Failing-first proof: `bundle://proof/SB02/recovery-lease-tests-failing-first.log`.
- Passing proof: `bundle://proof/SB02/recovery-lease-tests.log`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs`.
- Red-team negative case: two recovery scans run against the same live lease; the lease token remains non-empty and processing count stays zero.

## SB02-I2: expired leases can be released without duplicate enqueue

- Source raw note: expired leases can be recovered safely, but pending dispatch rows must not duplicate.
- Expected behavior: expired pending automation dispatch lease fields are cleared, the existing row is later processed once, and recovery does not enqueue a duplicate dispatch.
- Disallowed shallow implementation: leave expired stale lease tokens forever or enqueue another pending dispatch while one exists.
- Failing-first proof: `bundle://proof/SB02/recovery-lease-tests-failing-first.log`.
- Passing proof: `bundle://proof/SB02/recovery-lease-tests.log`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs`.
- Downstream dependency check: SB03 and SB07 can assume startup recovery does not steal live ownership.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProcessOutboxRecord.LeaseToken` / `LeaseExpiresAtUtc` | Outbox claim path in `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | Expired-only recovery path in `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs` | `bundle://proof/SB02/recovery-lease-tests.log` | `bundle://proof/SB02/recovery-lease-tests.log` live-lease test |

## Anti-stub proof

`bundle://proof/SB02/recovery-source-audit.log` verifies the production predicate uses `LeaseExpiresAtUtc.Value <= now` and no longer names live leases as stranded.
