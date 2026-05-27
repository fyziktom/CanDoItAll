# SB17: 17-refactor-checkpoint-b-process-runtime-cleanup

## Goal

Clean up process runtime after validation/status changes.

## Required work

- Extract shared diagnostic mapping helper if needed.
- Remove duplicated status mapping code.
- Keep API, UI, health, and finalizer semantics aligned.
- Run build and focused tests.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB17` are filled and the downstream dependency is safe.
