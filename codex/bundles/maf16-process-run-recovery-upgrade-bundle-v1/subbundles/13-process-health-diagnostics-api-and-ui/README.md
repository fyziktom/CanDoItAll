# SB13: 13-process-health-diagnostics-api-and-ui

## Goal

Expose diagnostics needed to debug future process runs.

## Required work

- The API currently reports `invariantDiagnosticCount` but captured response did not expose the diagnostic list as top-level data.
- Expose runtime invariant diagnostics or a focused endpoint for diagnostics.
- Expose artifact validation failure details in run detail and step detail.
- Expose storage/content-read problems separately from stale/wrong-run problems.
- Add UI or API readback tests.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB13` are updated and the next subbundle can safely depend on it.
