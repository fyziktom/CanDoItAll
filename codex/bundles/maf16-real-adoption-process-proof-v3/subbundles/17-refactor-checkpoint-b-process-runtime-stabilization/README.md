# SB17: 17-refactor-checkpoint-b-process-runtime-stabilization

## Goal

Stabilize runtime code after fixes.

## Required work

- Refactor duplicated path/content/hash logic.
- Document service boundaries.
- Run build and focused tests.
- Update skills/docs if behavior changes.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB17` are updated and downstream subbundles can rely on it.
