You are Codex working in `fyziktom/CanDoItAll`.

Target branch:
- `db-remove-sqlite`

Use the repository-local bundle skills:
- `codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
- `codex/skills/bundles/candoitall-bundle-execution/SKILL.md`

Execute this follow-up bundle:

`codex/bundles/candoitall-db-postgres-runtime-unblock-followup-bundle-v2`

High-level objective:
Finish the PostgreSQL-only runtime conversion and remove DB bottlenecks that are now unnecessary after SQLite removal, while preserving canonicality.

Hard constraints:
- Do not reintroduce SQLite.
- Do not modify `CanDoItAll.IPFS`.
- Keep all source-code comments in English.
- Do not hide retired-provider residue by string concatenation just to pass grep.
- Do not remove process/workflow/automation canonicality protections unless you replace them with durable PostgreSQL-backed claims and negative concurrency tests.
- Normal runtime must have one canonical database per process generation.
- No user/process/workflow operation may straddle two active database profiles.
- Hot database switching must become maintenance/restart-first unless explicitly feature-flagged for development.
- Do not implement portable DB snapshots in this pass.

Execute subbundles in order:
1. SB01 — Rebase, scope cleanup, and evidence hygiene
2. SB02 — Final legacy DB profile cleanup and quarantine hardening
3. SB03 — Canonical runtime database mode and pooled DbContext factory
4. SB04 — Convert hot DB switching to maintenance/restart-first flow
5. SB05 — PostgreSQL batch claim for automation/workflow/outbox delivery
6. SB06 — Process dispatch canonical leases without long in-memory guards
7. SB07 — Background job and database transfer boundary cleanup
8. SB08 — Final validation, benchmark, and merge gate

Critical proof required:
- Build passes.
- Unit tests pass.
- Relevant component tests pass.
- PostgreSQL-backed integration tests pass.
- Fresh PostgreSQL DB can be created from the single baseline migration.
- Legacy SQLite catalog quarantine test passes without crashing startup.
- Residue audit passes with an explicit allowlist; no hidden string concatenation.
- PostgreSQL concurrency tests prove no duplicate outbox/process/workflow claim.
- Benchmarks or timing evidence show the new normal DbContext path no longer passes through unnecessary runtime switch drain/lease overhead.
- Final report documents any remaining intentionally retained `InMemory` test-only surfaces.

Start by reading:
- `README.md`
- `analysis/01-branch-review.md`
- `analysis/03-db-bottleneck-inventory.md`
- `requirements/02-canonicality-invariants.md`
- `plan/01-phase-plan.md`
