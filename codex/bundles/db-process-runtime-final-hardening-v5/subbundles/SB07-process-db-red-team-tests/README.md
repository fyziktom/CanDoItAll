# SB07 - Process DB red-team tests

## Status

Completed.

## Objective

Prove process DB canonicality under adversarial concurrency and lease-loss conditions.

## Covered inputs

- User asked to preserve canonicality and verify process DB behavior.
- Conditional finalization and claim-first dispatch are implemented but need red-team proof.

## Exact source references

- `repo://tests/CanDoItAll.Tests.Integration/*`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`

## Required tests

1. Two workers claim the same process outbox record: only one finalizes.
2. Worker loses process outbox lease after side effect: finalization is suppressed and retry is idempotent.
3. Recovery worker sees a non-expired automation dispatch outbox lease: it does not clear it.
4. Recovery worker sees an expired automation dispatch lease: it can recover safely.
5. Long-running AgentFramework execution is heartbeat-renewed.
6. Stale process dispatch worker cannot project artifacts or transition step status.
7. Process candidate scan does not hydrate full candidate before durable claim.
8. Database profile pending activation does not affect canonical runtime workspace paths before restart.

## Do not do

- Do not use only mocked in-memory DB for canonicality tests.
- Do not assert only counts; assert concrete state, lease token, status, and journal/audit rows.
- Do not mark tests as quarantined without explicit reason.

## Acceptance checklist

- [x] PostgreSQL-backed red-team tests pass.
- [x] Tests fail against the old unsafe behavior.
- [x] Tests include negative duplicate/stale-worker cases.
- [x] Evidence includes command transcript.

## Proof required

- `proof/SB07/manifest.md`
- `proof/SB07/red-team-tests.log`
- `proof/SB07/semantic-invariants.md`

## Browser validation logging

N/A.

## Progression gate

SB08 merge readiness depends on this.

## Suggested agent prompt

Implement SB07. Add PostgreSQL-backed red-team tests for process DB leases, recovery, long-running dispatch, duplicate workers, and stale finalization.
