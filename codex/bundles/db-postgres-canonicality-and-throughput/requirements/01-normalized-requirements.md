# Normalized requirements

## R1 — Keep PostgreSQL-only runtime intact

Do not reintroduce SQLite provider, SQLite migrations, SQLite runtime snapshot DB, or SQLite compatibility logic.

## R2 — Remove leftover switching bottlenecks

Normal runtime `DbContext` creation must remain canonical and pooled. No normal path may acquire switch/drain leases.

## R3 — Preserve canonical truth

The running process has exactly one canonical runtime DB. Persisted activation for next restart is a separate state and must be named separately.

## R4 — Unlock PostgreSQL concurrency safely

Batch-claimed outbox/delivery records should be processed with bounded parallelism where canonicality permits. Parallelism must not create duplicate execution or split aggregate truth.

## R5 — Harden process dispatch ownership

Process automation must not commit after claim loss. Claim token must guard final mutations and artifact projection.

## R6 — Reduce candidate loading overhead

Process dispatch should avoid expensive full-run scans before durable claim. Move toward claim-first or two-stage claim/hydrate.

## R7 — Refresh validation proof

Run broad validation in a properly configured PostgreSQL environment or record exact blocker. Previous environment-limited validation is not a merge-ready proof.

## R8 — Clean branch scope

Decide whether bundle/proof artifacts belong in the branch. Remove unrelated proof artifacts if they are not intended as permanent repo content.
