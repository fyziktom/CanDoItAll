# SB01-rebase-scope-cleanup — Rebase, scope cleanup, and evidence hygiene

## Status

Prepared.

## Objective

Bring `db-remove-sqlite` current with `development` and decide whether generated bundle/proof artifacts belong in the merge branch.

## Covered Inputs

- User asked to review the latest `db-remove-sqlite` pass.
- User asked to identify DB bottlenecks left from SQLite-limit protection.
- User asked to preserve canonicality while unblocking throughput.

## Prerequisites

See `plan/01-phase-plan.md`.

## Exact Source References


- repo://CanDoItAll.slnx
- repo://01-execution-report.md
- repo://codex/bundles/db-remove-sqlite-followup-bundle-v1/reviews/01-execution-report.md
- repo://.codex/bundles/project-structure-workflow-runs/proof/**
- repo://codex/bundles/postgresql-only-main-runtime-bundle-v1/**
- repo://codex/bundles/db-remove-sqlite-followup-bundle-v1/**


## Deliverables


1. Rebase or merge latest `development` into `db-remove-sqlite`.
2. Re-run a minimal build after conflict resolution.
3. Decide branch artifact policy:
   - remove generated proof inputs from product branch, or
   - document why they are intentionally committed.
4. Ensure root `01-execution-report.md` reflects this branch, not stale unrelated process-agent work.
5. Produce a diff-scope report listing files intentionally retained.


## Dependency Impact

This subbundle may invalidate downstream proof if it changes runtime DB identity, process execution semantics, or validation scope. Do not proceed to dependent subbundles until the progression gate passes.

## Validation Depth

Build validation plus explicit diff-scope review.

## Implementation Steps


1. Rebase or merge latest `development` into `db-remove-sqlite`.
2. Re-run a minimal build after conflict resolution.
3. Decide branch artifact policy:
   - remove generated proof inputs from product branch, or
   - document why they are intentionally committed.
4. Ensure root `01-execution-report.md` reflects this branch, not stale unrelated process-agent work.
5. Produce a diff-scope report listing files intentionally retained.


## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not implement SQLite snapshots.

## Do Not Do

- Do not reintroduce SQLite runtime support.
- Do not weaken canonicality.
- Do not hide test failures behind broad "unrelated" claims.
- Do not remove locks before durable PostgreSQL claim proof exists.

## Acceptance Checklist


- [ ] Branch is no longer behind `development`.
- [ ] Scope report explains retained or removed bundle/proof artifacts.
- [ ] Root execution report is not stale/misleading.
- [ ] Build still passes after rebase/merge.


## Proof Required


- `proof/SB01-rebase-scope-cleanup/manifest.md`
- branch compare transcript
- build transcript
- artifact policy note


## Browser Validation Logging

Record route, viewport, actions, assertions, screenshot paths, and result when UI is touched. Use N/A only if this subbundle does not touch UI.

## Progression Gate

All acceptance checklist items and proof files must exist before starting downstream subbundles.

## Suggested Agent Prompt

Execute `SB01-rebase-scope-cleanup` from this bundle. Read the exact source references, implement only the scoped changes, then create proof under `proof/SB01-rebase-scope-cleanup/`.
