# SB17: 17-refactor-checkpoint-b-stabilization

## Goal

Refactor after all behavioral fixes.

## Required work

- Clean up partial classes if the MAF upgrade or artifact validation fixes created duplication.
- Document adapter seams and validation service boundaries.
- Run full build and focused tests again.
- Do not leave temporary compatibility hacks without tests.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB17` are updated and the next subbundle can safely depend on it.
