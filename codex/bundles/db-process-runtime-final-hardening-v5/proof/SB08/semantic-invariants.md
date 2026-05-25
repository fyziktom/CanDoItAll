# Semantic invariants SB08

## Status

Completed with classified broad-suite caveats.

## Invariants proved

- No stale worker may write canonical process DB state.
- Lease ownership must be explicit and verifiable.
- Retry behavior must be idempotent.
- PostgreSQL runtime must remain canonical.

## Negative proof

- SB07 red-team and SB08 focused process DB tests prove stale dispatch workers cannot finalize or project artifacts after losing a claim.
- SB02/SB08 recovery tests prove startup recovery preserves non-expired automation dispatch leases and only reclaims expired leases.
- SB04/SB08 idempotency tests prove duplicate pending automation dispatch commands are suppressed instead of creating extra side effects.
- SB05/SB08 query-plan proof rejects sequential scans on the process/automation/connector hot claim paths.

## Positive proof

- `bundle://proof/SB08/focused-process-db-tests.log`: 409 focused process DB integration tests passed.
- `bundle://proof/SB06/benchmark-output.json`: bounded parallel PostgreSQL processing completed all seeded records and captured duplicate-suppression/stale-finalization counters.
- `bundle://proof/SB05/query-plans/`: process outbox, automation delivery, connector command, and process step dispatch header query plans captured.
- `bundle://proof/SB08/ef-pending-model-changes.log`: no pending PostgreSQL model changes.
- `bundle://proof/SB08/runtime-residue-audit.log`: active SQLite runtime residue is limited to legacy quarantine identifiers.

## Production behavior artifact matrix

| Invariant | Producer | Consumer | Proof |
| --- | --- | --- | --- |
| Lease ownership remains explicit | Process outbox and process dispatch lease claims | Finalization and recovery paths | SB02, SB03, SB07, SB08 focused tests |
| Retry behavior remains idempotent | Stable process outbox idempotency keys and dedupe lookup | Activity/automation dispatch side effects | SB04 tests and SB06 duplicate metric |
| PostgreSQL claim throughput is canonical | PostgreSQL indexed `SKIP LOCKED` claims | Runtime workers | SB05 plans and SB06 benchmark |
| Stale finalization is visible | Runtime claim metrics | Operators/telemetry listeners | SB06 metrics listener proof |
