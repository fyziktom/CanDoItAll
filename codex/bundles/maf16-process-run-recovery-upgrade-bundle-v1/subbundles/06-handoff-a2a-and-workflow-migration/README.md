# SB06: 06-handoff-a2a-and-workflow-migration

## Goal

Upgrade and verify handoff, A2A, and workflows.

## Required work

- Compile and test `MafHandoffWorkflowFactory` and all workflow coordinator code.
- Account for A2A v1.0 migration and package/API changes.
- Verify handoff message roles are not mutated and CanDoItAll no longer relies on old behavior.
- Verify workflows assigned as process roles still route through process-owned artifact mapping/finalizer.
- Add workflow and A2A smoke tests or mark unsupported features explicitly with fallback behavior.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB06` are updated and the next subbundle can safely depend on it.
