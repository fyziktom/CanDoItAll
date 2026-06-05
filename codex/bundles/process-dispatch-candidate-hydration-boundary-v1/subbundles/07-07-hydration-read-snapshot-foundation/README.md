# SB07 - Introduce read snapshot records and loader cutline

## Status
Prepared.

## Objective
Introduce candidate hydration read snapshot and loader cutline.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
SB06 header migration passed.

## Exact Source References
- `ProcessRunAutomationDispatchService.Dispatch.cs`
- new hydration snapshot/loader helper files

## Deliverables
- Snapshot types for run, definition, dispatchable step, work brief, step definitions, assignments, artifacts, branch outcomes, artifact inputs.
- Loader may read EF but must not mutate.

## Dependency Impact
Prepares assembly movement without changing behavior yet.

## Validation Depth
Build + tests proving snapshot contains all required fields.

## Implementation Steps
1. 1. Create snapshot records.
2. Create loader that gathers read-only data.
3. Keep dispatcher consumer unchanged or minimally adapted.
4. Do not expose snapshot publicly.

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
Proof artifacts under `proof/SB07/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
SB08 Gate B may start when snapshot build/tests pass.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
