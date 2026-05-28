# SB16: 16-refactor-checkpoint-a-runtime-services

## Goal

Runtime service refactor checkpoint.

## Required work

- After SB02-SB08, refactor service boundaries: grounding, artifact validation, artifact identity/storage, manager resolution, run projection, process health.
- Reduce dispatch partial class bloat where safe.
- Run focused tests after refactor.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB16` are updated and the next dependent workstream can rely on it.
