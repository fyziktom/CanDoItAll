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
