# SB04: blazor-template-boundary-corrections

## Status

- Completed

## Objective

Correct Blazor templates before Tetris UI process testing.

## Covered Inputs

- RQ03 Blazor boundary correctness
- F02 Blazor validation/revalidation mutation drift

## Prerequisites

- SB03 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://Templates/Processes/processes/blazor-app-delivery
- repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs

## Scope

- Audit all `Templates/Processes/processes/blazor-*` definitions.
- Ensure architecture/intake steps are read-only and cannot mutate products.
- Ensure implementation and repair steps are the only steps with `MutateProductTarget` and `ExternalProductTargetMutable`.
- Ensure validation/revalidation steps are `ExternalProductTargetReadOnly` with `RunValidation`, `LaunchRuntime`, `CaptureRuntimeProof`, and `WriteManagedProcessArtifacts`, but no product mutation.
- Ensure final result/writeback/escalation steps do not mutate product source files. They may use `ExecuteExternalAction` for project-structure writeback and `WriteManagedProcessArtifacts` for evidence summaries.
- Add tests that fail if review/revalidation/escalation Blazor steps contain product mutation operations.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB04/.

## Implementation Steps

- Audit all `Templates/Processes/processes/blazor-*` definitions.
- Ensure architecture/intake steps are read-only and cannot mutate products.
- Ensure implementation and repair steps are the only steps with `MutateProductTarget` and `ExternalProductTargetMutable`.
- Ensure validation/revalidation steps are `ExternalProductTargetReadOnly` with `RunValidation`, `LaunchRuntime`, `CaptureRuntimeProof`, and `WriteManagedProcessArtifacts`, but no product mutation.
- Ensure final result/writeback/escalation steps do not mutate product source files. They may use `ExecuteExternalAction` for project-structure writeback and `WriteManagedProcessArtifacts` for evidence summaries.
- Add tests that fail if review/revalidation/escalation Blazor steps contain product mutation operations.

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
- bundle://proof/SB04/manifest.md and bundle://proof/SB04/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB04/manifest.md.
- Semantic invariant contract: bundle://proof/SB04/semantic-invariants.md.
- Command transcripts: bundle://proof/SB04/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate completed on 2026-05-26. Blazor boundary audit and projection regression proof exist under bundle://proof/SB04/, referenced paths resolve, and SB05 may rely on non-mutating Blazor validation/writeback/escalation steps.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB04 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB04` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
