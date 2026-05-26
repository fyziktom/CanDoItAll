# SB09: 09-workflow-subprocess-output-mapping-hardening

## Goal

Make required workflow/subprocess artifact mappings explicit.

## Required work

- Use `WorkflowOutputId`, `WorkflowOutputName`, `WorkflowOutputKind`, and `SubprocessChildArtifactExpectationId` for required artifact expectations.
- Update template projection and import/export to preserve these fields.
- Update linter to make missing mappings an error in strict mode.
- Add tests for ambiguous workflow outputs and ambiguous subprocess child artifacts.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB09` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
