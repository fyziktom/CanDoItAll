# SB08: nonsoftware-template-migration

## Status

- Completed

## Objective

Migrate business and generic templates to typed governance fields.

## Covered Inputs

- RQ02 typed template operation contracts
- F03 non-Blazor template migration

## Prerequisites

- SB07 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://Templates/Processes/processes/customer-onboarding
- repo://Templates/Processes/processes/business-plan-development
- repo://Templates/Processes/processes/incident-response

## Scope

- Update customer onboarding, business-plan-development, incident-response, architecture-decision-governance, release-readiness, OSS intake, and AI-assisted-change-delivery templates.
- For each step add `AllowedOperations` and `OperationTargetScope` appropriate for generic process semantics.
- Do not use software-specific operations for business/legal/incident steps unless the step really touches a product target.
- Add template validation tests that all templates in the manifest contain typed operation contracts.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB08/.

## Implementation Steps

- Update customer onboarding, business-plan-development, incident-response, architecture-decision-governance, release-readiness, OSS intake, and AI-assisted-change-delivery templates.
- For each step add `AllowedOperations` and `OperationTargetScope` appropriate for generic process semantics.
- Do not use software-specific operations for business/legal/incident steps unless the step really touches a product target.
- Add template validation tests that all templates in the manifest contain typed operation contracts.

## Scope Exceptions

- None planned. Any discovered exception must be recorded as a blocker, reopened subbundle, or concrete follow-up before closure.

## Do Not Do

- Do not hardcode Tetris behavior into generic process runtime code.
- Do not introduce SQLite paths or non-PostgreSQL persistence assumptions.
- Do not replace runtime proof with source-text-only assertions for behavior-changing work.
- Do not silently narrow raw notes that say all, every, must, or same flow.

## Acceptance Checklist

- Required work is implemented or explicitly blocked with a follow-up.
- Targeted tests and relevant audit commands pass.
- bundle://proof/SB08/manifest.md and bundle://proof/SB08/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB08/manifest.md.
- Semantic invariant contract: bundle://proof/SB08/semantic-invariants.md.
- Command transcripts: bundle://proof/SB08/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB08 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

Closed with strict governance audit and production template-pack regression proof in `bundle://proof/SB08/`. Downstream subbundles may rely on manifest templates having explicit typed operation contracts without reinterpreting prose.
