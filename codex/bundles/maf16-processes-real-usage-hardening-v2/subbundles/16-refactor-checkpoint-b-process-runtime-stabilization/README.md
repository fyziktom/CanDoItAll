# SB16: 16-refactor-checkpoint-b-process-runtime-stabilization

## Goal

Refactor process runtime seams after validation/recovery fixes.

## Required work

- Extract shared artifact validation service if still nested in dispatch partials.
- Clean up oversized partial classes and duplicated path/content/hash logic.
- Keep API, UI, dispatch, and recovery callers on the same validation service.
- Run build and focused tests.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB16` are updated and downstream subbundles can rely on the behavior.
