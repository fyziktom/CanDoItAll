# CanDoItAll DB Process Runtime Final Hardening Follow-up Bundle v5

## Status

Completed with classified broad-suite caveats.

SB02-SB07 process database hardening is implemented and validated. SB08 restore, build,
unit tests, EF drift, process DB focused tests, PostgreSQL query-plan proof, throughput
benchmark proof, and runtime residue audit completed. Broad component and integration
validation still has classified failures outside the process DB hardening touch set; see
`proof/SB08/final-execution-report.md` and `reviews/01-execution-report.md`.

## Target repository and branch

- Repository: `fyziktom/CanDoItAll`
- Target branch: `db-remove-sqlite`
- Base branch for comparison: `development`

## Objective

Review the current PostgreSQL-only runtime work, close remaining process-database canonicality gaps, and prove that process runtime database work is safe, canonical, and no longer constrained by SQLite-era serialization patterns.

This bundle is not another broad SQLite removal pass. SQLite is effectively removed from the typed main runtime surface. The remaining work is final hardening around:

- process outbox lease ownership,
- startup recovery lease reclamation,
- long-running process dispatch heartbeats,
- idempotent side effects,
- PostgreSQL claim/index/query-plan quality,
- measurable throughput proof,
- full merge readiness.

## Current review summary

The latest branch has made substantial progress:

- Runtime database switching/drain state was reduced to a canonical profile snapshot and restart-observed notification model.
- Normal runtime `AppDbContext` creation uses a pooled canonical DbContext factory.
- Profile-specific contexts are split into `IProfileAppDbContextFactory` and should be maintenance-only.
- PostgreSQL `FOR UPDATE SKIP LOCKED` batch claiming exists in automation delivery, connector outbox, and process outbox.
- Process dispatch now loads candidate headers, claims a step, then hydrates detailed candidates.
- Process outbox, connector outbox, and automation delivery finalization are now conditional on the active lease token.

Remaining critical risks:

1. Startup recovery can still clear non-expired process automation dispatch leases. That can break canonicality in multi-runtime or slow-start scenarios.
2. Process step dispatch claim renewal is callback-driven and may not be continuous during a long blocking AgentFramework execution run.
3. Process outbox side effects are at-least-once; canonical finalization is now safe, but duplicate side-effect semantics need explicit idempotency proof.
4. PostgreSQL claim queries need query-plan and index proof, not only source-level proof.
5. The latest report still lacks numeric throughput benchmark proof.
6. Broad integration/component validation caveats must be closed before merge, or converted to explicit quarantined issues with owner and reason.

## Subbundles

1. SB01 - Validation evidence and merge scope
2. SB02 - Startup recovery lease reclaim canonicality
3. SB03 - Long-running process dispatch heartbeat
4. SB04 - Process outbox idempotency and side-effect canonicality
5. SB05 - PostgreSQL process DB indexes and claim query plan
6. SB06 - Throughput benchmark and runtime metrics
7. SB07 - Process DB red-team tests
8. SB08 - Final merge readiness

## Hard rule

Do not weaken canonicality to increase throughput. Every parallel or recovery path must preserve a single canonical database owner for final state mutation.
