# SB13 - Introduce side-effect-explicit technical-agent binding coordinator

## Status
- Completed

## Objective
Introduce side-effect-explicit technical-agent binding coordinator.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
- Gate C passed.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`

## Deliverables
- `ProcessDispatchTechnicalAgentBindingCoordinator` or equivalent.
- Explicit outcome: missing binding, bound unchanged, access granted/saved, access already present, binding error.
- Tests for all outcomes.

## Dependency Impact
- Prepares direct-agent hydration migration. Side effects must be transparent.

## Validation Depth
- Focused unit/integration tests with fake bridge/execution client or existing test harness.

## Implementation Steps
1. Create coordinator with side-effect naming.
2. Keep project-structure access mutation testable.
3. Do not mark it pure.
4. Keep logging in dispatcher or return diagnostic strings.

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
- Proof artifacts under `proof/SB13/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
- N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
- SB14 may start after binding coordinator tests pass.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
