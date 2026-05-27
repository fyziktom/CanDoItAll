# SB10: 10-artifact-validation-status-model-expansion

## Goal

Expand artifact read model status beyond ContentUnavailable.

## Required work

- Add statuses or diagnostic categories for StaleOrWrongRun, WrongProducerMode, InvalidFormat, InsufficientEvidence, PlaceholderOnly, ContentHashMismatch.
- Preserve compact UI language but keep raw diagnostic available.
- Ensure enum changes are API-compatible or documented.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB10` are filled and the downstream dependency is safe.
