# SB02 - Startup recovery lease reclaim canonicality

## Status

Completed.

## Objective

Fix process recovery so startup scans cannot clear live, non-expired automation dispatch outbox leases.

## Covered inputs

- User asked to preserve canonicality and inspect process DB work.
- Current recovery code clears pending automation dispatch leases when `LeaseExpiresAtUtc > now`.

## Exact source references

- `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeOptions.cs`

## Problem

`ReleaseStrandedAutomationDispatchLeasesAsync` currently releases non-expired leases during startup recovery. A non-expired lease is canonical ownership. Without a lease owner/dead-instance proof, clearing it is unsafe.

## Implementation summary

Implemented the preferred immediate strategy. Startup recovery now releases only expired pending automation dispatch leases and leaves non-expired leases as canonical worker ownership.

## Deliverables

Implement one of these safe strategies:

### Preferred immediate strategy

- Remove non-expired lease release.
- Recovery may enqueue missing work only when no pending automation dispatch exists.
- Existing pending leased work is left alone until lease expiry.

### Optional advanced strategy

- Add `LeaseOwnerInstanceId` and `LeaseAcquiredAtUtc` to `ProcessOutboxRecord`.
- Only reclaim non-expired leases if the owner runtime instance is proven dead by a reliable runtime instance registry.
- Add PostgreSQL migration/baseline update and tests.

## Implementation steps

1. Replace `ReleaseStrandedAutomationDispatchLeasesAsync` with expired-only release, or remove it.
2. Update logs so they never call a non-expired lease stranded.
3. If owner metadata is added, update model, configuration, migration baseline, claim query, and finalization tests.
4. Add tests:
   - active non-expired lease is not cleared by startup recovery,
   - expired lease can be recovered,
   - recovery does not enqueue duplicate work when pending dispatch exists,
   - multi-worker scenario does not steal live lease.

## Do not do

- Do not clear non-expired leases just because this is the first startup recovery loop.
- Do not rely on machine name or process id alone as dead-instance proof.
- Do not reduce lease duration to hide the issue.

## Acceptance checklist

- [x] Non-expired leases remain untouched by recovery.
- [x] Expired leases are handled safely.
- [x] Tests prove live worker ownership is not stolen.
- [x] Recovery logs distinguish expired from non-expired leases.

## Proof required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- `proof/SB02/recovery-lease-tests.log`
- Source assertions showing no non-expired lease release.

## Browser validation logging

N/A.

## Progression gate

SB03 and SB07 may proceed only after this is proven.

## Suggested agent prompt

Implement SB02. Make startup process recovery canonical-safe: it must not release non-expired process automation dispatch leases unless owner death is proven by a reliable runtime instance registry.
