# SB01: 01-freeze-evidence-and-repro-baseline

## Goal

Freeze failed-run evidence and create a failing-first regression before upgrade.

## Required work

- Read `codex/bundles/process-run-first-step-artifact-binding-failure-inputs-v1/inputs/03-api-evidence-index.md` and raw API payloads.
- Create a test fixture or in-memory/postgres integration test that reproduces a current-run workspace-written artifact being rejected as `StaleOrWrongRun`.
- Assert the current contradictory state: artifact satisfaction says satisfied, finalizer says wrong run.
- Do not change runtime behavior in this subbundle except adding tests and fixtures.
- Record all source assertions and raw evidence paths.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB01` are updated and the next subbundle can safely depend on it.
