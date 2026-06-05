# SB18 - Final red-team and next safe cutline

## Status
Prepared.

## Objective
Final red-team and next safe cutline.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
SB01-SB17 complete.

## Exact Source References
- all changed source files
- execution report
- proof transcripts

## Deliverables
- Final red-team.
- Completed validator.
- Next cutline recommendation.
- Raw note closure.

## Dependency Impact
Determines whether a future bundle can approach Process Core readiness or should continue local isolation.

## Validation Depth
Full build, focused tests, source scans, bundle validator.

## Implementation Steps
1. 1. Run final scans.
2. Run final focused tests and build.
3. Validate bundle completed.
4. Decide next seam.
5. Record no-core/no-driver/no-viewport proof.

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
Proof artifacts under `proof/SB18/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
Bundle can close only if all raw notes are mapped and no scope drift exists.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
