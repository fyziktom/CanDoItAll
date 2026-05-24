# SB06-process-dispatch-durable-leases — Process dispatch canonical leases without long in-memory guards

## Status

Completed.

## Objective

Reduce in-memory per-step semaphore bottleneck by moving canonical process execution claim to PostgreSQL.

## Covered Inputs

- User asked to review the latest `db-remove-sqlite` pass.
- User asked to identify DB bottlenecks left from SQLite-limit protection.
- User asked to preserve canonicality while unblocking throughput.

## Prerequisites

See `plan/01-phase-plan.md`.

## Exact Source References


- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs
- repo://src/CanDoItAll.Modules.Processes/**
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs


## Deliverables


1. Map the current `StepDispatchGuards` behavior.
2. Define durable step execution claim fields or reuse existing fields:
   - claim token,
   - claimant id,
   - claimed timestamp,
   - lease expiry,
   - current execution run id,
   - attempt count.
3. Claim step execution atomically in PostgreSQL before starting long-running work.
4. Release in-memory semaphore before long external execution if a durable lease safely owns the work.
5. Keep short in-memory guard only around fast local claim/finalization sections if beneficial.
6. Preserve recovery/adoption paths for existing execution runs.
7. Add tests:
   - same step raced by multiple dispatchers -> one execution,
   - different steps -> can run concurrently,
   - stale execution lease -> recovery,
   - completion with stale token -> rejected.


## Dependency Impact

This subbundle may invalidate downstream proof if it changes runtime DB identity, process execution semantics, or validation scope. Do not proceed to dependent subbundles until the progression gate passes.

## Validation Depth

Critical foundation: multi-worker PostgreSQL integration tests and semantic proof.

## Implementation Steps


1. Map the current `StepDispatchGuards` behavior.
2. Define durable step execution claim fields or reuse existing fields:
   - claim token,
   - claimant id,
   - claimed timestamp,
   - lease expiry,
   - current execution run id,
   - attempt count.
3. Claim step execution atomically in PostgreSQL before starting long-running work.
4. Release in-memory semaphore before long external execution if a durable lease safely owns the work.
5. Keep short in-memory guard only around fast local claim/finalization sections if beneficial.
6. Preserve recovery/adoption paths for existing execution runs.
7. Add tests:
   - same step raced by multiple dispatchers -> one execution,
   - different steps -> can run concurrently,
   - stale execution lease -> recovery,
   - completion with stale token -> rejected.


## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not implement SQLite snapshots.

## Do Not Do

- Do not reintroduce SQLite runtime support.
- Do not weaken canonicality.
- Do not hide test failures behind broad "unrelated" claims.
- Do not remove locks before durable PostgreSQL claim proof exists.

## Acceptance Checklist


- [ ] Static semaphore is no longer the canonical protection.
- [ ] Long agent/workflow execution does not hold process-local guard unnecessarily.
- [ ] Durable PostgreSQL claim prevents duplicate execution across workers.
- [ ] Recovery behavior still works.
- [ ] Process artifacts/journal remain canonical.


## Proof Required


- `proof/SB06-process-dispatch-durable-leases/manifest.md`
- durable claim schema/behavior matrix
- integration test transcript
- negative race proof


## Browser Validation Logging

Record route, viewport, actions, assertions, screenshot paths, and result when UI is touched. Use N/A only if this subbundle does not touch UI.

## Progression Gate

All acceptance checklist items and proof files must exist before starting downstream subbundles.

## Suggested Agent Prompt

Execute `SB06-process-dispatch-durable-leases` from this bundle. Read the exact source references, implement only the scoped changes, then create proof under `proof/SB06-process-dispatch-durable-leases/`.
