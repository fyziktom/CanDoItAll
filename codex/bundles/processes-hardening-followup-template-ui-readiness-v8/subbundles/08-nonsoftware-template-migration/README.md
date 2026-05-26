# SB08: 08-nonsoftware-template-migration

## Goal

Migrate business and generic templates to typed governance fields.

## Required work

- Update customer onboarding, business-plan-development, incident-response, architecture-decision-governance, release-readiness, OSS intake, and AI-assisted-change-delivery templates.
- For each step add `AllowedOperations` and `OperationTargetScope` appropriate for generic process semantics.
- Do not use software-specific operations for business/legal/incident steps unless the step really touches a product target.
- Add template validation tests that all templates in the manifest contain typed operation contracts.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB08` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
