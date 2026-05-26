# SB11: refactor-checkpoint-b-runtime-validation-services

## Status

- Completed

## Objective

Refactor validation, lineage, and transition services for maintainability.

## Covered Inputs

- RQ07 unified artifact validation
- RQ08 workflow/subprocess mappings

## Prerequisites

- SB10 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch

## Scope

- Split oversized partial classes where needed.
- Create service boundaries for operation contract resolution, artifact validation, projection lineage, block/recovery routing, and template validation.
- Keep tests proving behavior unchanged after refactor.
- Do not move functionality into UI or template-only code.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB11/.

## Implementation Steps

- Split oversized partial classes where needed.
- Create service boundaries for operation contract resolution, artifact validation, projection lineage, block/recovery routing, and template validation.
- Keep tests proving behavior unchanged after refactor.
- Do not move functionality into UI or template-only code.

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
- bundle://proof/SB11/manifest.md and bundle://proof/SB11/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB11/manifest.md.
- Semantic invariant contract: bundle://proof/SB11/semantic-invariants.md.
- Command transcripts: bundle://proof/SB11/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB11 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB11` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

## Closure Notes

- Closed with adversarial and passing proof under `bundle://proof/SB11/transcripts/`.
- Production service-boundary evidence covers runtime block classification, health auditing, artifact identity, invariant auditing, recovery routing, workflow/subprocess mapping, the shared completion artifact validator, and the manual transition call site.
- Browser validation is not required for this backend/runtime checkpoint; SB15 remains the UI preflight phase.
