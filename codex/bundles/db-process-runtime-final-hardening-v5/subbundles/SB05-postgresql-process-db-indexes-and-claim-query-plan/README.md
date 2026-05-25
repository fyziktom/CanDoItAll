# SB05 - PostgreSQL process DB indexes and claim query plan

## Status

Completed.

## Objective

Prove that new PostgreSQL claim queries are supported by proper indexes and do not become a DB bottleneck.

## Covered inputs

- User asked to remove database bottlenecks.
- PostgreSQL `FOR UPDATE SKIP LOCKED` is present, but query-plan proof is missing.

## Exact source references

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs`
- `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`
- `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/*`

## Deliverables

1. Add or verify indexes for:
   - process outbox claim query,
   - process step dispatch claim/header query,
   - automation envelope delivery claim query,
   - connector command claim query.
2. Generate/adjust PostgreSQL baseline migration if indexes changed.
3. Produce `EXPLAIN (ANALYZE, BUFFERS)` proof for seeded datasets.
4. Add a regression test or script that verifies index presence.

## Implementation steps

- Seed datasets large enough to make plans meaningful.
- Capture query plans under `proof/SB05/query-plans/`.
- Prefer partial indexes where useful.
- Keep index count reasonable; do not add redundant indexes.

## Do not do

- Do not rely only on EF LINQ translation proof.
- Do not add indexes without query-plan proof.
- Do not forget that consolidated baseline migration must stay drift-free.

## Acceptance checklist

- [x] Claim queries have query-plan proof.
- [x] Hot paths do not do sequential scans on large seeded data unless justified.
- [x] EF pending model changes check passes.
- [x] Index inventory is documented.

## Implementation summary

- Added PostgreSQL raw migration indexes for process outbox, automation delivery, and connector command claim ordering.
- Verified the process step dispatch header path already uses the existing `IX_Processes_StepRuns_ProcessRunId_Sequence` index, avoiding a redundant new index.
- Added an integration regression that verifies index presence, seeds large datasets, captures `EXPLAIN (ANALYZE, BUFFERS)` plans, and rejects hot-table sequential scans.
- Captured EF pending model change proof and query plans under `proof/SB05/`.

## Proof required

- `proof/SB05/manifest.md`
- `proof/SB05/index-inventory.md`
- `proof/SB05/query-plans/*.txt`
- `proof/SB05/ef-pending-model-changes.log`

## Browser validation logging

N/A.

## Progression gate

SB06 numeric benchmark depends on this.

## Suggested agent prompt

Implement SB05. Add/verify PostgreSQL indexes for process/automation/connector claim queries and capture EXPLAIN ANALYZE proof on seeded data.
