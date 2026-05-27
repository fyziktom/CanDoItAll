# SB14: 14-recovery-and-operator-approval-correctness

## Goal

Prove recovery manager and operator approval cannot fake required artifacts.

## Required work

- Operator decision artifacts must remain decision evidence unless explicitly mapped to a decision expectation.
- Manager recovery must create/rebind the original required artifact with current-run lineage and content.
- Pending approval must not leave active/failed mixed state without clear next recovery action.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB14` are updated and downstream subbundles can rely on it.
