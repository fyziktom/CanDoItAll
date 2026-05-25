# candoitall-db-postgres-final-hardening-followup-bundle-v4

## Purpose

This follow-up bundle reviews the current `db-remove-sqlite` branch after the latest Codex implementation and defines the next hardening wave.

The previous wave removed the main SQLite runtime path and introduced PostgreSQL-only canonical runtime behavior, pooled canonical DbContext creation, restart-first database activation, PostgreSQL batch claiming, and durable process dispatch claims.

This bundle focuses on the remaining high-risk areas:

1. lease-based canonicality for long-running outbox and connector work,
2. real throughput defaults and benchmarks rather than only syntactic `FOR UPDATE SKIP LOCKED` proof,
3. final validation evidence quality,
4. merge-readiness after broad suite caveats,
5. preventing future agents from reintroducing multi-source database truth.

## Branch

- Repository: `fyziktom/CanDoItAll`
- Branch under review: `db-remove-sqlite`
- Comparison base: `development`
- Review timestamp: 2026-05-24T19:51:53Z

## High-level assessment

Codex fulfilled most of the previous bundle. The branch is now ahead of `development` and not behind. Canonical runtime/pending activation split is present. Hot-switch context drain was removed from the normal runtime. PostgreSQL claim patterns were added.

However, the next hardening step should not be another SQLite cleanup. The main risk is now **leased work finalization**: some workers claim rows safely, but then mutate tracked entities and `SaveChangesAsync` after external work. If a lease expires, is reclaimed, or heartbeat renewal fails, a stale worker may still write final state unless finalization is guarded by a conditional update or transaction boundary.

## Subbundles

| ID | Name | Purpose |
|---|---|---|
| SB01 | Merge evidence and residue cleanup | Verify branch currency, decide proof artifact retention, remove misleading stale bundle artifacts if required. |
| SB02 | Conditional finalization for leased outbox work | Add CAS-style finalization for connector/process/automation long-running leased work. |
| SB03 | Lease-loss hardening and heartbeat contracts | Treat heartbeat/renewal loss as a canonical stop condition, not a warning. |
| SB04 | Throughput defaults and runtime tuning | Ensure batch claiming actually runs with meaningful parallelism by default and is configurable safely. |
| SB05 | Benchmark and query-count proof | Add numeric throughput evidence and query-count/roundtrip proof. |
| SB06 | Process dispatch claim-first deep proof | Verify candidate loading is truly claim-first and not a hidden full-run bottleneck. |
| SB07 | PostgreSQL canonicality invariants and admin boundaries | Encode DB source-of-truth rules and maintenance profile boundaries in tests and docs. |
| SB08 | Final validation and merge readiness | Close broad-suite caveats, EF warnings inventory, and final merge gate. |

## Execution rule

Execute in order. SB02 and SB03 are critical foundations. Do not proceed to throughput tuning proof until leased finalization and lease-loss semantics are proven.
