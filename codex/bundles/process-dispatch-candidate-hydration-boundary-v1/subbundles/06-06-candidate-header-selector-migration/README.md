# SB06 - Migrate LoadDispatchCandidateHeadersAsync through selector

## Status
Prepared.

## Objective
Migrate `LoadDispatchCandidateHeadersAsync` through the header selector.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
SB05 selector foundation tests pass.

## Exact Source References
- `ProcessRunAutomationDispatchService.Dispatch.cs`
- candidate header selector helper

## Deliverables
- Dispatcher wrapper delegates to selector.
- Header parity proof.

## Dependency Impact
Candidate order and claim attempts depend on this.

## Validation Depth
Integration test for dispatchable candidates plus source scan for delegated wrapper.

## Implementation Steps
1. 1. Replace inline query with selector call.
2. Preserve return shape and ordering.
3. Preserve lease expiry and failed-run/in-progress exception.

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
Proof artifacts under `proof/SB06/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
SB07 may start only if header parity passes.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
