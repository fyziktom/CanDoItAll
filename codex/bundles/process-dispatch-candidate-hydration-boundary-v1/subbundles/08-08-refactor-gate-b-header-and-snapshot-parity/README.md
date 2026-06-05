# SB08 - Gate B candidate header/snapshot parity

## Status
Prepared.

## Objective
Refactor Gate B for header and read snapshot parity.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
SB07 complete.

## Exact Source References
- selector/helper tests
- hydration snapshot tests
- `Dispatch.cs` wrapper source

## Deliverables
- Gate B transcript.
- Line counts.
- No-core/no-driver/no-viewport scans.

## Dependency Impact
Unlocks candidate assembly movement.

## Validation Depth
Focused dispatch tests + architecture tests + build.

## Implementation Steps
1. 1. Run header selector parity.
2. Run snapshot/loader tests.
3. Run source scans.
4. Record reopen triggers.

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
Proof artifacts under `proof/SB08/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
Gate B must pass before SB09.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
