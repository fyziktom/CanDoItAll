# Repo Hygiene Test Migration Stabilization

This bundle is a follow-up stabilization package for the broken test, migration, and repository hygiene signals observed after recent CanDoItAll architecture and tool changes.

## Profile

- `feedback`

## Mission

- Restore a trustworthy validation baseline by repairing obsolete or broken hygiene tests, runtime launch/watch tests, process-template assertions, branch-signal recovery tests, and database migration/isolation checks without weakening the guards that protect repository quality.

## Outcome Contract

- Requested outcome: the affected unit-test slices pass, EF model/migration checks are clean, the full unit suite can run without the known hygiene failures or hangs, and the `5032` app can be rebuilt, started, and smoke-tested.
- Hard constraints: do not paper over failing hygiene tests with broad allowlists; do not add a PostgreSQL migration unless `dotnet ef migrations has-pending-model-changes` fails after isolation fixes; do not change production behavior only to satisfy stale prose assertions; preserve current filesystem bundle work and unrelated user changes.
- Evidence required before closure: failing-first transcripts for each repaired failure cluster, passing targeted test transcripts, EF pending-model check transcript, full unit-suite transcript or documented remaining unrelated failure, build transcript, and browser/API smoke proof for `localhost:5032`.
- Known blockers or explicit scope exceptions: none remaining. The full-suite run exposed a real composition/snapshot drift after isolation was repaired; the final EF pending-model check is clean.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-tracked-artifact-and-test-naming-hygiene`
2. `subbundles/02-runtime-launch-and-watch-restore-tests`
3. `subbundles/03-process-template-and-branch-signal-drift`
4. `subbundles/04-database-migration-and-test-isolation`
5. `subbundles/05-full-suite-and-5032-smoke-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Implemented`
- Subbundle gate review: `Completed`
- Final closure gate: `Passed`
- Browser validation analytics: `HTTP smoke passed for localhost:5032`
