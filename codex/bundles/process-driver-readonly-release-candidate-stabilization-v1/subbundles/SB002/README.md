# SB002 - Rerun or record build/full-unit/focused/source-scan baseline and stale-debt audit

## Status
- Completed
- Closure proof is recorded in undle://reviews/01-execution-report.md and the nearest critical proof manifest.

## Objective
Rerun or record build/full-unit/focused/source-scan baseline and stale-debt audit

## Covered Inputs
- Raw request to verify latest pushed `maf-processes-refactor` work using real code.
- Current state source artifacts listed in `bundle://inputs/source-artifacts.md`.

## Prerequisites
- Previous subbundles in numeric order are complete.
- If any source scan or focused test fails, stop and repair before continuing.
- Critical gates must not be bypassed by downstream proof.

## Exact Source References
- repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationBatchOrchestrator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationPayloadBuilder.cs
- repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs

## Deliverables
- Source changes or documentation changes required by this subbundle.
- Tests and proof artifacts scoped to this subbundle.
- Updated execution report row.

## Dependency Impact
- Downstream phases rely on this subbundle for release-candidate stability of the read-only driver pipeline.
## Validation Depth
- Focused source-backed validation. Use nearest critical gate for broad closure.
## Implementation Steps
1. Re-read the exact source files before changing anything.
2. Implement the smallest complete change for this objective.
3. Preserve all hard constraints: no runtime host, registry, selector, DI, manager command, scheduler/workflow hook, file/network/storage/workspace access, process mutation, claim/transition/finalizer/retry mutation, shell/Graph calls, or Core driver dependency.
4. Add or update focused tests.
5. Record proof under `proof/SB002/`.

## Scope Exceptions
No UI/mobile/small/medium/browser proof unless UI/media drift occurs unexpectedly. Unexpected UI/media drift fails this subbundle.

## Do Not Do
- Do not add generic `Verify(object)` or string-lane dispatch.
- Do not add runtime host, registry, selector, DI, service registration, hosted service, scheduler hook, workflow hook, or manager command.
- Do not read files, call network/Office/Graph, write workspace/storage, or mutate process state.
- Do not move runtime side effects into Core.
- Do not close this subbundle with report-only proof.

## Acceptance Checklist
- [x] Behavior is source-backed, not report-only.
- [x] Focused tests pass.
- [x] Source scans pass.
- [x] No UI/media drift.
- [x] No stub/TODO/NotImplemented production markers.
- [x] Execution report row updated.


## Proof Required
- `proof/SB002/manifest.md`
- `proof/SB002/semantic-invariants.md` for critical gates
- Command transcript paths
- Source assertion paths
- Changed-file hashes when production files changed
- Anti-stub scan output

## Browser Validation Logging
- N/A backend/runtime/Core/driver work. If UI/media files change, stop and re-scope.
## Progression Gate
- May continue after focused proof passes, but nearest critical gate must validate cumulative behavior.
## Suggested Agent Prompt
Implement `SB002 - Rerun or record build/full-unit/focused/source-scan baseline and stale-debt audit` exactly as specified. Preserve the read-only driver boundary and record artifact-backed proof before marking the row complete.


