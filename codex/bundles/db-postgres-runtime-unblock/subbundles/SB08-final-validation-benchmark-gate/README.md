# SB08-final-validation-benchmark-gate — Final validation, benchmark, and merge gate

## Status

Prepared.

## Objective

Prove the branch is merge-ready and that DB hot-path bottlenecks were actually removed.

## Covered Inputs

- User asked to review the latest `db-remove-sqlite` pass.
- User asked to identify DB bottlenecks left from SQLite-limit protection.
- User asked to preserve canonicality while unblocking throughput.

## Prerequisites

See `plan/01-phase-plan.md`.

## Exact Source References


- repo://CanDoItAll.slnx
- repo://src/**
- repo://tests/**
- repo://codex/bundles/**


## Deliverables


1. Run final residue audit with explicit allowlist.
2. Run build.
3. Run unit tests.
4. Run targeted component tests.
5. Run PostgreSQL integration tests.
6. Run Playwright Data Sources tests.
7. Run fresh PostgreSQL baseline migration proof.
8. Run concurrency tests for outbox/process claims.
9. Capture a short before/after or diagnostic proof for DbContext creation path:
   - no profile resolution per context,
   - no runtime switch lease per context,
   - pooled/canonical factory used.
10. Write final merge-readiness report with open risks.


## Dependency Impact

This subbundle may invalidate downstream proof if it changes runtime DB identity, process execution semantics, or validation scope. Do not proceed to dependent subbundles until the progression gate passes.

## Validation Depth

Full final validation gate.

## Implementation Steps


1. Run final residue audit with explicit allowlist.
2. Run build.
3. Run unit tests.
4. Run targeted component tests.
5. Run PostgreSQL integration tests.
6. Run Playwright Data Sources tests.
7. Run fresh PostgreSQL baseline migration proof.
8. Run concurrency tests for outbox/process claims.
9. Capture a short before/after or diagnostic proof for DbContext creation path:
   - no profile resolution per context,
   - no runtime switch lease per context,
   - pooled/canonical factory used.
10. Write final merge-readiness report with open risks.


## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not implement SQLite snapshots.

## Do Not Do

- Do not reintroduce SQLite runtime support.
- Do not weaken canonicality.
- Do not hide test failures behind broad "unrelated" claims.
- Do not remove locks before durable PostgreSQL claim proof exists.

## Acceptance Checklist


- [ ] Branch is current with development.
- [ ] Build passes.
- [ ] Unit/component/integration tests pass or quarantines are justified.
- [ ] Residue audit passes honestly.
- [ ] Fresh PostgreSQL baseline proof passes.
- [ ] Concurrency tests pass.
- [ ] DbContext hot-path bottleneck proof passes.
- [ ] Final report lists remaining risks and merge recommendation.


## Proof Required


- `proof/SB08-final-validation-benchmark-gate/manifest.md`
- full command transcripts
- browser screenshots
- benchmark/diagnostic note
- final merge-readiness report


## Browser Validation Logging

Record route, viewport, actions, assertions, screenshot paths, and result when UI is touched. Use N/A only if this subbundle does not touch UI.

## Progression Gate

All acceptance checklist items and proof files must exist before starting downstream subbundles.

## Suggested Agent Prompt

Execute `SB08-final-validation-benchmark-gate` from this bundle. Read the exact source references, implement only the scoped changes, then create proof under `proof/SB08-final-validation-benchmark-gate/`.
