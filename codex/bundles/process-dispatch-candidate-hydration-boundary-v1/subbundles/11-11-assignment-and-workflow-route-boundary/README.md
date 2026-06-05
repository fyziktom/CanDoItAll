# SB11 - Move current assignment/workflow route recognition

## Status
Prepared.

## Objective
Extract current assignment and workflow route recognition.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
SB10 complete.

## Exact Source References
- `ResolveDispatchCurrentAssignment` usage
- `IsWorkflowDispatchAssignment` usage

## Deliverables
- Assignment/workflow route helper.
- Tests for workflow executor kind, workflow definition id, preferred executor kind, preferred workflow id.

## Dependency Impact
Incorrect detection changes route from workflow to agent or vice versa.

## Validation Depth
Focused workflow route parity tests.

## Implementation Steps
1. 1. Create helper that consumes loaded assignment/role facts.
2. Preserve existing wrappers.
3. Do not call workflow runtime here.

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
Proof artifacts under `proof/SB11/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
SB12 Gate C may start when workflow route parity passes.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
