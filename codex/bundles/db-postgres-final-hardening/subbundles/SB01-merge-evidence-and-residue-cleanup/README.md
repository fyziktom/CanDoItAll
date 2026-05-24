# SB01 — Merge evidence and residue cleanup

## Status

Completed.

## Objective

Confirm branch currency, decide what proof artifacts should remain in the product repo, and document any stale evidence that should not be treated as runtime source.

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


- `repo://codex/bundles/db-postgres-canonicality-and-throughput/reviews/01-execution-report.md`
- `repo://codex/bundles/db-postgres-canonicality-and-throughput/proof/SB08/final-execution-report.md`
- `.codex/bundles/project-structure-workflow-runs/**`
- `codex/bundles/**`


## Deliverables


1. Run `git fetch origin` and prove `origin/development` is an ancestor of `HEAD`.
2. Check whether large proof artifacts should remain committed or be moved to `.gitignore`, docs archive, or external evidence storage.
3. Mark old bundle reports as historical if they are retained.
4. Ensure root `01-execution-report.md` is not stale or misleading.


## Dependency Impact

This subbundle affects downstream trust in throughput/canonicality proof.

## Validation Depth

Critical where indicated. Use source audit, focused tests, broad validation when possible, and anti-stub checks.

## Implementation Steps


1. Run `git fetch origin` and prove `origin/development` is an ancestor of `HEAD`.
2. Check whether large proof artifacts should remain committed or be moved to `.gitignore`, docs archive, or external evidence storage.
3. Mark old bundle reports as historical if they are retained.
4. Ensure root `01-execution-report.md` is not stale or misleading.


## Scope Exceptions

None unless explicitly documented in proof.

## Do Not Do

- Do not hide failures behind focused tests only.
- Do not claim throughput improvement without either numeric benchmark or clearly stated limitation.
- Do not introduce new non-canonical DB source-of-truth.


## Acceptance Checklist


- [ ] Branch is not behind `origin/development`.
- [ ] Historical bundle/evidence artifacts are either intentionally retained or removed.
- [ ] Execution report clearly distinguishes current proof from old proof.


## Proof Required


- `proof/SB01/manifest.md`
- transcript for `git status`, `git merge-base --is-ancestor`, and artifact-retention decision


## Browser Validation Logging

N/A unless Data Sources UI or runtime/pending activation display changes.

## Progression Gate

This subbundle is complete only when proof artifacts are written under `proof/` and downstream subbundles can rely on its claims.

## Suggested Agent Prompt

Execute this subbundle exactly. Preserve canonical runtime DB invariants and create artifact-backed proof before moving to the next subbundle.
