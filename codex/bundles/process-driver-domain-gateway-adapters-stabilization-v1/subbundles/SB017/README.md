# SB017 - Add explicit Office/business gateway methods

## Status
- Completed

## Objective
Implement the `Add explicit Office/business gateway methods` slice in phase `P06 Gateway implementation for artifact/Office/business` while preserving read-only driver boundaries and stable Process Core.

## Covered Inputs
- `inputs/raw-request.md`
- `inputs/source-artifacts.md`
- `requirements/01-normalized-requirements.md`

## Prerequisites
- All previous subbundles in `plan/01-phase-plan.md` must have passed.
- For critical gates, all upstream critical proof manifests must exist.

## Exact Source References
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Drivers.Abstractions
- repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification
- repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence
- repo://src/CanDoItAll.Processes.Drivers.ArtifactEvidence
- repo://src/CanDoItAll.Processes.Drivers.OfficeEvidence
- repo://src/CanDoItAll.Processes.Drivers.BusinessAnalysis
- repo://src/CanDoItAll.Processes.Drivers.ObservationAggregation
- repo://src/CanDoItAll.Processes.Drivers.VerificationGateway
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch
- repo://tests/CanDoItAll.Tests.Unit
- repo://tests/CanDoItAll.Tests.Integration

## Deliverables
- Source/test/doc updates for this subbundle only.
- Updated proof artifacts under `proof/SB017/`.
- Execution report row updated for this exact subbundle.

## Dependency Impact
- This subbundle may invalidate downstream phases if it changes package dependencies, public APIs, gateway lane semantics, evidence policy, or process adapter boundaries.

## Validation Depth
- Focused validation plus source scan. Downstream critical gate will aggregate proof.

## Implementation Steps
1. Re-read live source before editing.
2. Apply the smallest coherent change that completes this subbundle objective.
3. Update or add tests before claiming completion.
4. Run focused tests and source scans.
5. If this is a critical gate, run build and broader focused matrix.

## Scope Exceptions
No runtime host, registry, selector, DI, manager command, scheduler/workflow hook, shell execution, Graph call, workspace/storage write, process mutation, claim/transition/finalizer/retry mutation, or UI/media work is allowed.

## Do Not Do
- Do not add generic `Verify(lane, object)` dispatch.
- Do not weaken source scans with broad allow-lists.
- Do not mark skipped tests as solved without owner/reopen trigger.
- Do not use report-only proof.

## Acceptance Checklist
- [ ] Source changed only within allowed scope.
- [ ] Tests cover positive and negative behavior.
- [ ] No forbidden runtime/API/dependency drift.
- [ ] Execution report row updated.
- [ ] Proof artifacts created.

## Proof Required
- Build/focused test transcript or documented deferral to the next critical gate.
- Source assertions.
- Anti-stub scan.
- Changed-file hashes.


## Browser Validation Logging
- N/A unless UI/media files change. If UI/media files change, fail and re-scope rather than adding small/medium/mobile proof.

## Progression Gate
- Do not proceed to downstream phases unless this subbundle row is passed and downstream dependencies are checked.

## Suggested Agent Prompt
Implement `SB017 - Add explicit Office/business gateway methods` from `process-driver-domain-gateway-adapters-stabilization-v1`. Preserve hard constraints. Use live source, tests, and proof artifacts; do not rely on report-only status.
