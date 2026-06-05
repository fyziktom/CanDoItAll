# SB10 - Move branch outcome/conditional dependency shaping

## Status
- Completed

## Objective
Extract branch outcome and conditional dependency context shaping.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
- SB09 complete.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`

## Deliverables
- Module-local branch/dependency context helper.
- Tests for explicit branch outcome requirement detection.

## Dependency Impact
- Branch routing and finalizer branch selection depend on this.

## Validation Depth
- Focused branch outcome tests.

## Implementation Steps
1. Extract available branch outcomes.
2. Extract requires-explicit-branch-outcome flag.
3. Preserve display order and ids.

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
- Proof artifacts under `proof/SB10/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
- N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
- SB11 may start when branch/dependency parity passes.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
