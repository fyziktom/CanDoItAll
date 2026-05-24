# SB05 — Benchmark and query-count proof

## Status

Completed.

## Objective

Replace source-only throughput claims with numeric evidence.

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


- `repo://tests/CanDoItAll.Tests.Integration/**`
- `repo://codex/bundles/db-postgres-canonicality-and-throughput/proof/SB08/benchmark-report.md`


## Deliverables


1. Add a lightweight integration benchmark or diagnostic test marked appropriately.
2. Measure sequential/single parallelism vs configured bounded parallelism for process outbox, connector outbox, and automation delivery if practical.
3. Capture query counts or at least major DB roundtrips for process dispatch candidate loading.
4. Store numbers in a benchmark report with environment details.
5. Keep benchmark non-flaky; do not make absolute time thresholds strict unless environment-controlled.


## Dependency Impact

This subbundle affects downstream trust in throughput/canonicality proof.

## Validation Depth

Critical where indicated. Use source audit, focused tests, broad validation when possible, and anti-stub checks.

## Implementation Steps


1. Add a lightweight integration benchmark or diagnostic test marked appropriately.
2. Measure sequential/single parallelism vs configured bounded parallelism for process outbox, connector outbox, and automation delivery if practical.
3. Capture query counts or at least major DB roundtrips for process dispatch candidate loading.
4. Store numbers in a benchmark report with environment details.
5. Keep benchmark non-flaky; do not make absolute time thresholds strict unless environment-controlled.


## Scope Exceptions

None unless explicitly documented in proof.

## Do Not Do

- Do not hide failures behind focused tests only.
- Do not claim throughput improvement without either numeric benchmark or clearly stated limitation.
- Do not introduce new non-canonical DB source-of-truth.


## Acceptance Checklist


- [ ] Benchmark report includes counts and timings.
- [ ] Query/roundtrip proof exists for claim-first dispatch.
- [ ] Duplicate-execution negative tests pass under parallel load.


## Proof Required


- `proof/SB05/benchmark-report.md`
- benchmark transcript
- duplicate negative test transcript


## Browser Validation Logging

N/A unless Data Sources UI or runtime/pending activation display changes.

## Progression Gate

This subbundle is complete only when proof artifacts are written under `proof/` and downstream subbundles can rely on its claims.

## Suggested Agent Prompt

Execute this subbundle exactly. Preserve canonical runtime DB invariants and create artifact-backed proof before moving to the next subbundle.
