# SB02 - Live inventory of candidate header selection and hydration

## Status
- Completed

## Objective
Build live source-backed inventory of candidate header selection and hydration responsibilities.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
- SB01 completed.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`

## Deliverables
- Updated `inventories/02-candidate-hydration-method-map.md`.
- Exact source ranges for header selection, hydration data reads, candidate creation, binding, recovery query, and access mutation.

## Dependency Impact
- Downstream helper design depends on this inventory; stale inventory reopens SB02.

## Validation Depth
- Source scan with method/range classification; no production movement.

## Implementation Steps
1. Map every query and side-effect in `LoadDispatchCandidateHeadersAsync` and `LoadDispatchCandidateAsync`.
2. Categorize pure shaping vs EF read vs side effect.
3. Record the test coverage slice for each category.

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
- Proof artifacts under `proof/SB02/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
- N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
- SB03 may start only when every hydration section is categorized.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
