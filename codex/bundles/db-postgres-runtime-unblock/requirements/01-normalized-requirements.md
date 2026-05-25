# Normalized requirements

## R1 — Reconcile branch state

`db-remove-sqlite` must be rebased or merged with latest `development` before final validation.

## R2 — Complete legacy DB cleanup

No production `src/`, `tests/`, `CanDoItAll.slnx`, build, or runtime path may contain retired SQLite provider support. Legacy catalog quarantine may retain retired-provider detection only as an explicit allowlisted compatibility boundary.

## R3 — Honest residue audit

Do not hide retired-provider words via string concatenation. The final audit must allow intentional quarantine strings through an explicit allowlist.

## R4 — PostgreSQL-only persistent runtime

The only persistent runtime provider is PostgreSQL. `InMemory` may remain only for explicit test/runtime override scenarios and must not be user-managed as a persisted Data Sources profile.

## R5 — Canonical runtime database

A running process has exactly one canonical runtime database profile per generation. Normal app work must not dynamically resolve and switch databases on every DbContext.

## R6 — Remove normal-path switch bottleneck

Normal `AppDbContext` creation must not pass through the global switch/drain gate. Use pooled canonical PostgreSQL contexts for hot path work.

## R7 — Move DB switching to maintenance/restart flow

Persisting a new active profile should require restart by default. In-process hot switching may exist only as a development-only or maintenance-mode feature with explicit warnings and proof.

## R8 — Durable PostgreSQL claims

Workflow/process/automation work claims must be canonical across processes, not only protected by static in-memory semaphores. PostgreSQL-backed locks/leases/unique constraints must provide correctness.

## R9 — Batch claim where PostgreSQL enables it

Outbox/message/workflow queues must use PostgreSQL batch claim patterns instead of single-row sequential claim loops when safe.

## R10 — Preserve canonicality while unblocking throughput

Every throughput optimization must include negative tests proving:
- no duplicate process step execution,
- no duplicate outbox delivery execution,
- stale lease rescue works,
- concurrent workers do not produce split-brain results.

## R11 — Clean merge scope

Generated proof artifacts and old prepared bundles must either be removed from the feature branch or explicitly documented as intentionally committed.

## R12 — Final proof

Final proof must include build, unit tests, targeted component tests, PostgreSQL integration tests, fresh migration baseline proof, residue audit, concurrency tests, and a short before/after bottleneck analysis.
