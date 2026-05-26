# SB06: refactor-checkpoint-a-contract-normalization

## Status

- Completed

## Objective

Refactor operation contract normalization into one authoritative service.

## Covered Inputs

- RQ02 typed template operation contracts
- RQ03 Blazor boundary correctness

## Prerequisites

- SB05 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OperationContractResolver.cs

## Scope

- Expand `ProcessStepOperationContractState` beyond sorting/deduping.
- Centralize target-scope implied operations, invalid combinations, default operation sets by step kind, and strict validation.
- Use the same normalizer in editor save, import/export, template projection, lint, dispatch metadata, and tests.
- Remove or clearly mark legacy text inference as fallback only.
- Run focused tests before continuing.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB06/.

## Implementation Steps

- Expand `ProcessStepOperationContractState` beyond sorting/deduping.
- Centralize target-scope implied operations, invalid combinations, default operation sets by step kind, and strict validation.
- Use the same normalizer in editor save, import/export, template projection, lint, dispatch metadata, and tests.
- Remove or clearly mark legacy text inference as fallback only.
- Run focused tests before continuing.

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
- bundle://proof/SB06/manifest.md and bundle://proof/SB06/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB06/manifest.md.
- Semantic invariant contract: bundle://proof/SB06/semantic-invariants.md.
- Command transcripts: bundle://proof/SB06/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate passed: proof artifacts exist under `bundle://proof/SB06/`, referenced paths resolve, focused tests pass, and downstream dependency impact is recorded in `bundle://reviews/01-execution-report.md`.
- Dependent subbundles may rely on `ProcessStepOperationContractState` as the authoritative operation-contract normalization and validation surface.

## Suggested Agent Prompt

- Execute SB06 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB06` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
