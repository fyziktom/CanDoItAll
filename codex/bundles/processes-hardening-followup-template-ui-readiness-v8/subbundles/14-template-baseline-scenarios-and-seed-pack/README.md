# SB14: template-baseline-scenarios-and-seed-pack

## Status

- Completed

## Objective

Extend baseline scenarios to cover typed templates.

## Covered Inputs

- RQ02 typed template operation contracts
- RQ04 Tetris WASM PWA readiness

## Prerequisites

- SB13 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://Templates/Processes/seed-catalog/baseline-scenarios.json
- repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs

## Scope

- Add or update baseline scenarios for Blazor WASM PWA/Tetris, customer onboarding, business plan, incident response, release readiness, and architecture decision governance.
- Each baseline should exercise artifact creation, branch selection, block/recovery state, and typed operation contracts.
- Ensure scenario data is generic and reusable.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB14/.

## Implementation Steps

- Add or update baseline scenarios for Blazor WASM PWA/Tetris, customer onboarding, business plan, incident response, release readiness, and architecture decision governance.
- Each baseline should exercise artifact creation, branch selection, block/recovery state, and typed operation contracts.
- Ensure scenario data is generic and reusable.

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
- bundle://proof/SB14/manifest.md and bundle://proof/SB14/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB14/manifest.md.
- Semantic invariant contract: bundle://proof/SB14/semantic-invariants.md.
- Command transcripts: bundle://proof/SB14/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB14 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB14` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

## Closure Notes

- Added generic baseline scenario coverage for release readiness/deployment and architecture decision governance.
- Extended required baseline scenarios with typed contract and recovery exercises.
- Updated runtime seed replay to preserve typed blocked causes and to avoid expectation collisions when reusing seeded artifacts.
- Validated with the focused SB14 integration set and recorded proof under `bundle://proof/SB14/`.
