# SB05-postgresql-batch-claim-outbox — PostgreSQL batch claim for automation/workflow/outbox delivery

## Status

Prepared.

## Objective

Replace sequential or SQLite-era claim loops with PostgreSQL atomic batch claim patterns.

## Covered Inputs

- User asked to review the latest `db-remove-sqlite` pass.
- User asked to identify DB bottlenecks left from SQLite-limit protection.
- User asked to preserve canonicality while unblocking throughput.

## Prerequisites

See `plan/01-phase-plan.md`.

## Exact Source References


- repo://src/CanDoItAll.Modules.Automation/**
- repo://src/CanDoItAll.Modules.Processes/**
- repo://src/CanDoItAll.Modules.SchedulerPlanner/**
- repo://tests/CanDoItAll.Tests.Integration/AutomationRuntimeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/**


## Deliverables


1. Inventory all queue/outbox/delivery claim paths.
2. Remove any remaining SQLite-specific branches such as `Database.IsSqlite()`.
3. Implement PostgreSQL batch claim using `FOR UPDATE SKIP LOCKED` / `UPDATE ... RETURNING` where safe.
4. Keep attempt count, lease token, stale lease rescue, and terminal state semantics.
5. Avoid holding DB transactions during external agent/plugin execution.
6. Add concurrent-worker tests.
7. Add metrics/logging for claimed batch size and skipped locked rows if useful.


## Dependency Impact

This subbundle may invalidate downstream proof if it changes runtime DB identity, process execution semantics, or validation scope. Do not proceed to dependent subbundles until the progression gate passes.

## Validation Depth

Critical foundation: PostgreSQL-backed concurrency tests with multiple workers.

## Implementation Steps


1. Inventory all queue/outbox/delivery claim paths.
2. Remove any remaining SQLite-specific branches such as `Database.IsSqlite()`.
3. Implement PostgreSQL batch claim using `FOR UPDATE SKIP LOCKED` / `UPDATE ... RETURNING` where safe.
4. Keep attempt count, lease token, stale lease rescue, and terminal state semantics.
5. Avoid holding DB transactions during external agent/plugin execution.
6. Add concurrent-worker tests.
7. Add metrics/logging for claimed batch size and skipped locked rows if useful.


## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not implement SQLite snapshots.

## Do Not Do

- Do not reintroduce SQLite runtime support.
- Do not weaken canonicality.
- Do not hide test failures behind broad "unrelated" claims.
- Do not remove locks before durable PostgreSQL claim proof exists.

## Acceptance Checklist


- [ ] Due deliveries can be claimed in batches.
- [ ] Concurrent workers do not duplicate delivery handling.
- [ ] Stale leases are rescued only after timeout.
- [ ] Non-stale running deliveries are not stolen.
- [ ] Delivery attempt count is monotonic.
- [ ] No DB transaction is held during long external execution.


## Proof Required


- `proof/SB05-postgresql-batch-claim-outbox/manifest.md`
- SQL/EF implementation notes
- concurrent integration test transcript
- negative duplicate-claim proof


## Browser Validation Logging

Record route, viewport, actions, assertions, screenshot paths, and result when UI is touched. Use N/A only if this subbundle does not touch UI.

## Progression Gate

All acceptance checklist items and proof files must exist before starting downstream subbundles.

## Suggested Agent Prompt

Execute `SB05-postgresql-batch-claim-outbox` from this bundle. Read the exact source references, implement only the scoped changes, then create proof under `proof/SB05-postgresql-batch-claim-outbox/`.
