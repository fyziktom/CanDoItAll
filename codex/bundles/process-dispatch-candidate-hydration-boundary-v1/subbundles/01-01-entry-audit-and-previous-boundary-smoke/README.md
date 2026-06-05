# SB01 - Entry audit, branch hygiene, previous claim/route boundary smoke

## Status
Prepared.

## Objective
Establish current branch baseline after the claim/route bundle and prove previous boundaries still pass.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
Branch must be clean except intended work.

## Exact Source References
- `repo://codex/bundles/process-dispatch-claim-route-boundary-v1/reviews/01-execution-report.md`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`

## Deliverables
- Baseline diff/status proof.
- Current line counts for Dispatch/Concurrency/Finalizer and candidate hydration region.
- Previous no-core/no-driver/no-viewport proof preserved.

## Dependency Impact
No downstream code movement may start if the previous boundary is broken.

## Validation Depth
Source scans + architecture smoke + full or focused build as appropriate.

## Implementation Steps
1. 1. Capture branch status.
2. Capture line counts.
3. Run no-core/no-driver/no-viewport scans.
4. Run focused architecture smoke.
5. Record proof in `proof/SB01`.

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
Proof artifacts under `proof/SB01/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
SB02 may start only if baseline proof is clean.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
