# SB03 - Design local selector/loader/assembler/coordinator seams

## Status
Prepared.

## Objective
Define module-local candidate hydration seam design and migration cutline.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-structured-input.md`

## Prerequisites
SB02 inventory complete.

## Exact Source References
- `architecture/02-candidate-hydration-staging.md`
- `inventories/02-candidate-hydration-method-map.md`

## Deliverables
- Design note for selector, loader, assembler, artifact input helper, assignment resolver, binding coordinator.
- Explicit side-effect naming for technical-agent binding coordinator.

## Dependency Impact
Wrong seam design invalidates all movement after SB04.

## Validation Depth
Design review + source assertion; no production behavior movement.

## Implementation Steps
1. 1. Confirm which helper may read EF and which must be pure.
2. Confirm side-effect boundary for project-structure read-access mutation.
3. Confirm no public contract promotion.

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
Proof artifacts under `proof/SB03/` with source assertions, transcripts, and semantic invariants.

## Browser Validation Logging
N/A expected - service/runtime refactor only. Record `N/A` in the execution report. If UI changes unexpectedly, stop and require large desktop/PC-only proof.

## Progression Gate
SB04 may start when seam cutline is documented and reviewed.

## Suggested Agent Prompt
Implement this subbundle only. Preserve behavior, keep changes module-local, update proof artifacts, and do not start downstream subbundles until this progression gate passes.
