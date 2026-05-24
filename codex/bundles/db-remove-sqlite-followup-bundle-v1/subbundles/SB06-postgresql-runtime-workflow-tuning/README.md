# SB06 - PostgreSQL runtime workflow/process tuning

## Goal

Use PostgreSQL capabilities now that SQLite no longer constrains durable process/workflow execution.

## Context

The first pass mostly removed SQLite infrastructure. It did not clearly prove PostgreSQL-specific workflow/process/outbox concurrency tuning.

## Required changes

1. Audit durable execution files with:
   - process run dispatch,
   - process outbox/side effects,
   - workflow runtime persistence,
   - automation envelopes,
   - plugin command outbox,
   - scheduler/planner durable jobs,
   - background job tracking if used as durable execution.
2. Identify loops that claim queued work by read-then-update without transactional row locking.
3. Implement PostgreSQL-safe claim patterns:
   - transaction boundary,
   - row-level lock,
   - `FOR UPDATE SKIP LOCKED` or equivalent Npgsql raw SQL claim,
   - idempotency keys,
   - clear lease/heartbeat/expiry semantics where needed.
4. Do not replace everything with raw SQL; use raw SQL only for atomic claim primitives where EF cannot express the required locking.
5. Raise worker concurrency where it was artificially low only because SQLite existed, but keep sensible limits configurable.
6. Add negative tests:
   - two workers cannot process the same item,
   - failed worker releases/renews claim correctly,
   - duplicate dispatch is idempotent,
   - process/workflow side effects are not double-applied.

## Validation

- PostgreSQL-backed integration tests with parallel workers pass repeatedly.
- No provider-neutral SQLite-era workaround remains in durable claim paths.
- Logs/evidence show concurrency tests and any chosen worker concurrency setting.

## Proof artifacts

Write:

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- relevant logs under `evidence/SB06/`

## Acceptance criteria

- Durable process/workflow execution is safer and more parallel on PostgreSQL.
- Tests would fail on double-claim/double-dispatch regressions.

## Status

- Completed

## Objective

Audit and tune durable runtime concurrency now that persisted runtime execution targets PostgreSQL.

## Covered Inputs

- `bundle://requirements/01-followup-requirements.md`

## Prerequisites

- SB05 PostgreSQL baseline proof completed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeOptions.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs`

## Deliverables

- Typed worker concurrency defaults and clamp constants.
- Negative concurrency regression tests.
- Audit note that existing durable claims already use atomic EF update/lease/idempotency patterns.

## Dependency Impact

- Process outbox worker configuration and scheduler/planner durable dispatch tests are affected.

## Validation Depth

- Non-quarantined integration suite and targeted runtime concurrency slices.

## Implementation Steps

- Audit durable dispatch paths.
- Raise process outbox concurrency safely.
- Add process and scheduler concurrency tests.

## Do Not Do

- Do not replace existing safe EF atomic claims with raw SQL unless EF cannot express the claim primitive.

## Acceptance Checklist

- Default process outbox concurrency is greater than one and bounded.
- Duplicate dispatch tests pass.
- Non-quarantined integration tests pass.

## Proof Required

- `bundle://proof/SB06/manifest.md`

## Browser Validation Logging

- No browser route is required for this subbundle.

## Progression Gate

- Integration proof must pass before cleanup/final validation.

## Suggested Agent Prompt

Implement SB06, then run the runtime integration proof commands in `proof/SB06/manifest.md`.
