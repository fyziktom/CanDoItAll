# SB09: 09-refactor-checkpoint-a-agent-runtime-adapter

## Goal

Refactor adapter seams after MAF proof.

## Required work

- Consolidate MAF feature detection/adoption helpers.
- Keep MAF internals out of Processes module.
- Document adoption/deferred matrix in code docs or codex skill docs.
- Run focused MAF tests.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB09` are filled and the downstream dependency is safe.
