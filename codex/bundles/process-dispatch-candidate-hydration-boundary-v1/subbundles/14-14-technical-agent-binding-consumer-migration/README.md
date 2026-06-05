# SB14 - Use binding coordinator in direct-agent hydration

## Status
- Completed

## Objective
Migrate direct-agent candidate hydration to use technical-agent binding coordinator.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
- SB13 complete.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`

## Deliverables
- Direct-agent hydration uses binding outcome.
- Missing binding skip behavior preserved.
- Access grant/save behavior preserved.

## Dependency Impact
- This is a side-effectful movement; must be proven carefully.

## Validation Depth
- Focused direct-agent hydration tests and source scans.

## Implementation Steps
1. Replace inline binding summary/editor/save code with coordinator call.
2. Preserve warning diagnostic.
3. Preserve access grant and SaveAgentAsync.
4. Preserve candidate construction fields.

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
- Proof artifacts under `proof/SB14/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
- N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
- SB15 may start when direct-agent parity passes.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
