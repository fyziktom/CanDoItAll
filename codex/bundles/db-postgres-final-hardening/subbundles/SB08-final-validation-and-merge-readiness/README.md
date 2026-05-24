# SB08 — Final validation and merge readiness

## Status

Prepared.

## Objective

Close broad validation caveats and produce merge-ready evidence.

## Covered Inputs

- User requested review of what Codex fulfilled and skipped.
- User requested removal of DB bottlenecks left from SQLite-era protection.
- User requested preserving canonical database source-of-truth.

## Prerequisites

- Work from branch `db-remove-sqlite`.
- Do not reintroduce SQLite runtime provider, migrations, or UI.
- Keep code comments in English.
- Read `codex/skills/bundles/candoitall-bundle-execution/SKILL.md` before implementation.

## Exact Source References


- `repo://CanDoItAll.slnx`
- `repo://tests/**`
- `repo://codex/bundles/db-postgres-canonicality-and-throughput/proof/SB08/final-execution-report.md`


## Deliverables


1. Run restore/build.
2. Run full unit tests.
3. Run broad integration suite with correct PostgreSQL credentials.
4. Run broad component suite or explicit quarantine list.
5. Run EF pending model changes check.
6. Run residue audit for SQLite, hot-switch, drain, fake-proof, stale labels, source-of-truth drift.
7. Produce final merge-readiness report.


## Dependency Impact

This subbundle affects downstream trust in throughput/canonicality proof.

## Validation Depth

Critical where indicated. Use source audit, focused tests, broad validation when possible, and anti-stub checks.

## Implementation Steps


1. Run restore/build.
2. Run full unit tests.
3. Run broad integration suite with correct PostgreSQL credentials.
4. Run broad component suite or explicit quarantine list.
5. Run EF pending model changes check.
6. Run residue audit for SQLite, hot-switch, drain, fake-proof, stale labels, source-of-truth drift.
7. Produce final merge-readiness report.


## Scope Exceptions

None unless explicitly documented in proof.

## Do Not Do

- Do not hide failures behind focused tests only.
- Do not claim throughput improvement without either numeric benchmark or clearly stated limitation.
- Do not introduce new non-canonical DB source-of-truth.


## Acceptance Checklist


- [ ] Broad integration suite is green or quarantined with exact owner/reason.
- [ ] Broad component suite is green or quarantined with exact owner/reason.
- [ ] EF model has no pending changes.
- [ ] Final report lists remaining risks honestly.


## Proof Required


- `proof/SB08/manifest.md`
- final execution report
- test transcripts
- residue audit transcript


## Browser Validation Logging

N/A unless Data Sources UI or runtime/pending activation display changes.

## Progression Gate

This subbundle is complete only when proof artifacts are written under `proof/` and downstream subbundles can rely on its claims.

## Suggested Agent Prompt

Execute this subbundle exactly. Preserve canonical runtime DB invariants and create artifact-backed proof before moving to the next subbundle.
