# SB09: 09-artifact-current-run-binding-and-path-normalization

## Goal

Fix current-run binding and managed path normalization.

## Required work

- Normalize both `artifacts/process-runs/{runId}/...` and `artifacts/scopes/{scope}/{scopeId}/process-runs/{runId}/...` as current-run artifact paths when they belong to the same run.
- Ensure external reference key path mismatch does not override explicit run/step/expectation/execution lineage.
- Do not accept unrelated organization-scoped stale files just because they contain the run id string.
- Add tests for current-run org-scoped path, stale run path, wrong step path, wrong expectation id, wrong execution run id, and workflow/subprocess lineage.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB09` are updated and the next subbundle can safely depend on it.
