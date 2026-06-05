# SB17 - Documentation-only driver readiness candidate/evidence map

## Status
- Completed

## Objective
Document candidate/evidence driver readiness map without production APIs.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
- Gate D passed.

## Exact Source References
- `bundle://architecture/03-driver-readiness-position.md`
- `bundle://inventories/03-driver-readiness-candidate-map.md`

## Deliverables
- Documentation-only map of candidate facts to future driver needs.
- Explicit non-goals and no-driver scan.

## Dependency Impact
- Prepares later driver conversation without freezing API prematurely.

## Validation Depth
- Docs/source scan proving no production driver files/interfaces/DI registrations.

## Implementation Steps
1. Update driver readiness map with actual helper names and candidate facts.
2. State what remains module-local.
3. Do not add code for drivers.

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
- Proof artifacts under `proof/SB17/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
- N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
- SB18 may start after no-driver scan passes.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
