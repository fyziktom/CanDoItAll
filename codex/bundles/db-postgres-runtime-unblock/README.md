# CanDoItAll DB Remove SQLite Follow-up Bundle v2

## Status

Implemented on `fyziktom/CanDoItAll`, branch `db-remove-sqlite`, with documented external validation blockers.

Final closure state:

- Source changes for SB02-SB07 are implemented.
- Build, unit tests, targeted component tests, focused PostgreSQL integration tests, Playwright Data Sources proof, EF model drift proof, residue audit, source assertions, and anti-stub audit are captured under `proof/`.
- `git fetch origin` is blocked on this machine by SSH public key authentication; local `development` ancestry is proven.
- The broad non-quarantined integration command timed out after local PostgreSQL default-user authentication failures in unrelated integration setup; the changed runtime/profile/outbox/process surfaces passed the focused integration sweep.

## Objective

Close the remaining PostgreSQL-only runtime work after the second SQLite-removal pass, then remove PostgreSQL-era bottlenecks that are still shaped like SQLite-era protections.

This bundle has two goals:

1. Finish correctness cleanup:
   - reconcile `db-remove-sqlite` with latest `development`,
   - remove or explicitly justify remaining legacy database profile states,
   - harden legacy catalog quarantine,
   - clean scope/evidence artifacts,
   - prove the branch is merge-ready.

2. Unblock PostgreSQL runtime throughput while preserving canonicality:
   - stop resolving/building DbContext options for every normal context,
   - avoid global runtime-switch drain in normal DB operations,
   - keep one canonical runtime database per process generation,
   - replace in-memory/sequential dispatch bottlenecks with durable PostgreSQL claims,
   - ensure process/workflow/automation execution cannot double-claim canonical work.

## Current review summary

Codex improved the branch substantially. SQLite provider/source enum values and the typed SQLite connection model were removed. Snapshot runtime service was deleted. The Data Sources UI no longer offers SQLite or snapshot controls. The branch still needs a focused pass because the current runtime keeps switchability and context-drain mechanics that are now heavier than necessary for PostgreSQL-only canonical operation.

## Key source observations

- `DatabaseProfileModels.cs` now exposes only `PostgreSql` and `InMemory`, and source kinds are only `PostgresConnection` and `InMemory`.
- `LegacyDatabaseProfileCatalogQuarantine.cs` protects startup from legacy catalogs, but hides the retired provider token using string concatenation. This makes residue audits less honest.
- `SwitchableAppDbContextFactory` still resolves the current profile and builds DbContext options per context.
- `DatabaseRuntimeSwitching.cs` still takes a lease for every current-profile context and drains all active contexts before hot switch.
- `DatabaseSwitchCoordinator` still implements live switching with a fixed drain timeout.
- Process automation still has static per-step semaphores around long-running execution paths. This protects canonicality but can serialize too much work inside one process.
- The execution report says in-scope tests passed, but full component tests had unrelated timeout/failure notes and the branch is currently diverged from `development`.

## Required execution style

Use repository-local bundle skills and execute subbundles in order. Do not implement throughput optimizations before the canonicality contract is written and tested.

## Subbundles

1. SB01 — Rebase, scope cleanup, and evidence hygiene
2. SB02 — Final legacy DB profile cleanup and quarantine hardening
3. SB03 — Canonical runtime database mode and pooled DbContext factory
4. SB04 — Convert hot DB switching to maintenance/restart-first flow
5. SB05 — PostgreSQL batch claim for automation/workflow/outbox delivery
6. SB06 — Process dispatch canonical leases without long in-memory guards
7. SB07 — Background job and database transfer boundary cleanup
8. SB08 — Final validation, benchmark, and merge gate

## Non-goals

- Do not reintroduce SQLite.
- Do not implement portable DB snapshots now.
- Do not modify `CanDoItAll.IPFS`.
- Do not remove canonicality guards without durable PostgreSQL replacement proof.
- Do not make hidden string concatenation the way to pass residue audits.

## Final Proof Index

- Execution report: `reviews/01-execution-report.md`
- Final proof manifest: `proof/SB08-final-validation-benchmark-gate/manifest.md`
- Changed-file hash inventory: `proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv`
- Bundle validator: `scripts/validate_bundle.py`
