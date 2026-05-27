# SB13: 13-readmodel-finalizer-parity-and-health-details

## Goal

Make step detail health match finalizer validation.

## Required work

- Step detail artifact satisfaction should expose exact validation status, not only Satisfied/Missing.
- Run health missing/invalid artifact counts should match validation results.
- Add tests where an artifact is recorded but invalid and the UI/API does not show it as fully satisfied.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB13` are updated and downstream subbundles can rely on it.
