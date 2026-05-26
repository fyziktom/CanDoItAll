# SB12: 12-recovery-lifecycle-and-manager-approval-routing

## Goal

Fix recovery behavior for artifact binding failures.

## Required work

- Artifact binding validation failures should become actionable recovery state, not opaque process failure when recovery is possible.
- Manager recovery must create or rebind current-run evidence with lineage, not just an operator decision artifact.
- Pending `processes_artifact_record` approval in manager chat must not be mistaken for recovered required artifact unless it satisfies the exact expectation.
- Add tests for recovery manager accepting a valid re-created artifact and rejecting an operator-decision artifact that does not satisfy the step expectation.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB12` are updated and the next subbundle can safely depend on it.
