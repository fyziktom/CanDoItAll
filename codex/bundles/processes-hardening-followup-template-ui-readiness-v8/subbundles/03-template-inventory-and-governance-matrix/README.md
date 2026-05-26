# SB03: template-inventory-and-governance-matrix

## Status

- Completed

## Objective

Build an explicit matrix for every template in `Templates/Processes/manifest.json`.

## Covered Inputs

- RQ02 typed template operation contracts
- F03 mixed template migration state

## Prerequisites

- SB02 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://Templates/Processes/manifest.json
- repo://Templates/Processes/processes

## Scope

- List every template key from the manifest.
- For every step, record whether it has `AllowedOperations`, `OperationTargetScope`, branch outcomes, required artifacts, artifact inputs, and exception policy.
- Mark templates as ready/not-ready for strict typed governance.
- Fail the subbundle if any template still relies on prose-only operation boundaries without a planned migration.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB03/.

## Implementation Steps

- List every template key from the manifest.
- For every step, record whether it has `AllowedOperations`, `OperationTargetScope`, branch outcomes, required artifacts, artifact inputs, and exception policy.
- Mark templates as ready/not-ready for strict typed governance.
- Fail the subbundle if any template still relies on prose-only operation boundaries without a planned migration.

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
- bundle://proof/SB03/manifest.md and bundle://proof/SB03/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB03/manifest.md.
- Semantic invariant contract: bundle://proof/SB03/semantic-invariants.md.
- Command transcripts: bundle://proof/SB03/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate completed on 2026-05-26. Matrix proof exists under bundle://proof/SB03/, referenced paths resolve, and SB04/SB06/SB08 may rely on the typed-contract gap inventory.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB03 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB03` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
