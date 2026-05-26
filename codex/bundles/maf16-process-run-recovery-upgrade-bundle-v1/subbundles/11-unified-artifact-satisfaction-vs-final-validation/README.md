# SB11: 11-unified-artifact-satisfaction-vs-final-validation

## Goal

Make artifact satisfaction read-model and finalizer validation use shared semantics.

## Required work

- Extract or reuse one artifact validation service for both step read-model satisfaction and finalizer completion gate.
- Ensure run health missing artifact count agrees with finalizer validation.
- If a finalizer rejects an artifact, the UI/API must not simultaneously show it as fully satisfied without diagnostic qualifiers.
- Expose satisfaction status levels such as `Satisfied`, `ContentInvalid`, `StaleOrWrongRun`, `ContentUnavailable`, and `ValidationFailed` where appropriate.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB11` are updated and the next subbundle can safely depend on it.
