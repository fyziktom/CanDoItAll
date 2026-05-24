# SB07 - Unrelated change and evidence cleanup

## Goal

Keep the `db-remove-sqlite` branch focused and mergeable.

## Context

The branch includes unrelated/stale-looking artifacts such as the root `01-execution-report.md` and `.codex/bundles/project-structure-workflow-runs/proof/...` files.

## Required changes

1. Review every changed file from `git diff --name-status development..db-remove-sqlite`.
2. Categorize each changed file:
   - required SQLite/PostgreSQL-only change,
   - required follow-up bundle/evidence,
   - unrelated change to revert/move,
   - stale report to update.
3. Pay special attention to:
   - `.codex/bundles/project-structure-workflow-runs/proof/...`,
   - root `01-execution-report.md`,
   - `ManagedSeedProviderFallbacks.cs`,
   - agent runtime factory/helper changes,
   - docs that mention DB provider assumptions.
4. Remove or justify unrelated artifacts in the final execution report.
5. Keep `codex/bundles/postgresql-only-main-runtime-bundle-v1` only if this repo intentionally stores bundle artifacts; otherwise move to evidence/archive according to project convention.

## Validation

- `git diff --name-status development..HEAD` has no unexplained unrelated files.
- Execution report lists all non-code artifacts kept intentionally.
- Build/tests still pass after cleanup.

## Proof artifacts

Write:

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- relevant logs under `evidence/SB07/`

## Acceptance criteria

- Branch is focused and reviewable.
- No stale report claims unrelated work as current work.
