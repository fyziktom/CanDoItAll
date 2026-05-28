# SB03: 03-artifact-validation-readmodel-and-health-consolidation

## Goal

Ensure artifact validation, read model, health, API, and UI share semantics.

## Required work

- Create or finalize a shared artifact validation/status projection service.
- Audit every status: Expected, Satisfied, AutoProjected, Missing, ProjectionFailed, ContentUnavailable, InvalidFormat, InsufficientEvidence, StaleOrWrongRun, WrongProducerMode, PlaceholderOnly, ContentHashMismatch.
- Ensure health and recovery classification consume the same status mapping.
- Add matrix tests for every status and every surface.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB03` are updated and the next dependent workstream can rely on it.
