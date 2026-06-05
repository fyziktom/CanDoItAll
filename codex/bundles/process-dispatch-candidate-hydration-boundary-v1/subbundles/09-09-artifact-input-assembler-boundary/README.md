# SB09 - Move artifact-input prompt shaping behind local helper

## Status
Prepared.

## Objective
Extract artifact-input prompt shaping from candidate hydration.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
Gate B passed.

## Exact Source References
- `BuildResolvedArtifactInputs` and `PrepareArtifactInputsForPrompt` consumers
- new artifact input assembler helper

## Deliverables
- Module-local artifact input assembler.
- Parity tests for managed paths, source step mapping, missing/available inputs.

## Dependency Impact
Prompt semantics and artifact satisfaction depend on this.

## Validation Depth
Focused artifact-input tests and snapshot comparison.

## Implementation Steps
1. 1. Move shaping logic behind helper while keeping wrappers.
2. Preserve path normalization and upstream artifact summaries.
3. Keep file/storage side effects out.

## Scope Exceptions
- Do not create Process Core.
- Do not create production process-driver APIs.
- Do not perform unrelated cleanup.
- Do not change UI or run small/medium/mobile proof.

## Do Not Do
- Do not move EF writes, workflow/subprocess/execution-client/finalizer calls into pure helpers.
- Do not rename process tools or alter access/approval policy.
- Do not hide technical-agent/project-structure access mutation behind a pure planner.

## Acceptance Checklist
- [ ] Source changes stay under the named module-local boundary.
- [ ] Existing dispatcher wrapper methods remain available unless explicitly replaced with parity proof.
- [ ] Focused tests pass.
- [ ] Full build or required gate build passes when this subbundle is a gate.
- [ ] No Process Core, no driver API, no prohibited viewport artifacts.

## Proof Required
Proof artifacts under `proof/SB09/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
SB10 may start after artifact input parity passes.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
