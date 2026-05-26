# SB09: workflow-subprocess-output-mapping-hardening

## Status

- Completed

## Objective

Make required workflow/subprocess artifact mappings explicit.

## Covered Inputs

- RQ08 workflow/subprocess mappings

## Prerequisites

- SB08 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor

## Scope

- Use `WorkflowOutputId`, `WorkflowOutputName`, `WorkflowOutputKind`, and `SubprocessChildArtifactExpectationId` for required artifact expectations.
- Update template projection and import/export to preserve these fields.
- Update linter to make missing mappings an error in strict mode.
- Add tests for ambiguous workflow outputs and ambiguous subprocess child artifacts.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB09/.

## Implementation Steps

- Use `WorkflowOutputId`, `WorkflowOutputName`, `WorkflowOutputKind`, and `SubprocessChildArtifactExpectationId` for required artifact expectations.
- Update template projection and import/export to preserve these fields.
- Update linter to make missing mappings an error in strict mode.
- Add tests for ambiguous workflow outputs and ambiguous subprocess child artifacts.

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
- bundle://proof/SB09/manifest.md and bundle://proof/SB09/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB09/manifest.md.
- Semantic invariant contract: bundle://proof/SB09/semantic-invariants.md.
- Command transcripts: bundle://proof/SB09/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB09 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

Closed with strict lint, template-projection, and mapper ambiguity proof in `bundle://proof/SB09/`. Downstream subbundles may rely on workflow/subprocess required artifact mappings being explicit and lint-enforced.
