# SB04: 04-blazor-template-boundary-corrections

## Goal

Correct Blazor templates before Tetris UI process testing.

## Required work

- Audit all `Templates/Processes/processes/blazor-*` definitions.
- Ensure architecture/intake steps are read-only and cannot mutate products.
- Ensure implementation and repair steps are the only steps with `MutateProductTarget` and `ExternalProductTargetMutable`.
- Ensure validation/revalidation steps are `ExternalProductTargetReadOnly` with `RunValidation`, `LaunchRuntime`, `CaptureRuntimeProof`, and `WriteManagedProcessArtifacts`, but no product mutation.
- Ensure final result/writeback/escalation steps do not mutate product source files. They may use `ExecuteExternalAction` for project-structure writeback and `WriteManagedProcessArtifacts` for evidence summaries.
- Add tests that fail if review/revalidation/escalation Blazor steps contain product mutation operations.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB04` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
