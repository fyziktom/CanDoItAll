# SB05: 05-output-grounding-and-final-delivery-contract-refactor

## Goal

Refactor output grounding and final external delivery proof into a dedicated service.

## Required work

- Extract project-structure grounding path parsing/scoring from dispatch partial into a service.
- Create typed models for external target hints, required output roots, confidence, source node, and reason.
- Add adversarial tests for unrelated paths, sibling architecture branches, nested delivery targets, Windows paths, escaped paths, invalid annotations, and possible future Unix/URL path support.
- Ensure prompts require final delivery proof only when a credible grounded external target exists.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: runtime / API / UI / docs / template / test-harness / MAF.
- Note whether this closes previous proof debt.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB05` are updated and the next dependent workstream can rely on it.
