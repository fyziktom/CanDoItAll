# SB12: 12-satisfaction-readmodel-finalizer-parity

## Goal

Ensure read model and finalizer agree on artifact status.

## Required work

- Refactor read-model satisfaction to call the same validator or shared service.
- Expose partial statuses like ContentUnavailable, WrongProducerMode, StaleOrWrongRun in API/UI.
- Fail tests if step detail says Satisfied while finalizer would reject.
- Update health missing artifact count to include invalid current-step artifact state.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB12` are updated and downstream subbundles can rely on the behavior.
