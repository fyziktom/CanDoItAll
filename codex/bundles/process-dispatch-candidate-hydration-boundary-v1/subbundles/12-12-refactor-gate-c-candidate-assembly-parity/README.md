# SB12 - Gate C candidate assembly parity

## Status
- Completed

## Objective
Refactor Gate C for candidate assembly parity.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
- SB09-SB11 complete.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`

## Deliverables
- Gate C proof across subprocess, workflow-backed role, and direct-agent candidates.
- Full/targeted build.
- No-core/no-driver/no-viewport scans.

## Dependency Impact
- Unlocks side-effectful technical-agent boundary work.

## Validation Depth
- Focused integration slices + source scans.

## Implementation Steps
1. Run candidate assembly tests.
2. Compare dispatch candidate fields.
3. Record line counts and reopened risks.

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
- Proof artifacts under `proof/SB12/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
- N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
- Gate C must pass before SB13.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
