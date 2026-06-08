# SB038 — Harden business-analysis read-only lane denial tests and docs.

## Status
Prepared.

## Objective
Harden business-analysis read-only lane denial tests and docs.

## Covered Inputs
- Raw request: crash-aware review and next bundle toward stable Process Core with domain drivers.
- Normalized requirements: see `bundle://requirements/01-normalized-requirements.md`.
- Phase: `P13 — Office/Business Read-Only Denial Hardening`.

## Prerequisites
- Previous subbundle: `SB037` must be closed unless this is SB001.
- For critical gates, all earlier subbundles in the phase must have source-backed proof.

## Exact Source References
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTranscriptVerificationReadOnlyAdapterTests.cs`
- `repo://src/CanDoItAll.Processes.Core/Execution/ProcessExecutionEvidenceDescriptors.cs`
- `repo://src/CanDoItAll.Processes.Core/Finalization/ProcessFinalizerEvidenceDescriptors.cs`
- `repo://src/CanDoItAll.Processes.Core/Diagnostics/ProcessRetryDiagnosticDescriptors.cs`
- `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactProjectionEvidenceDescriptors.cs`

## Deliverables / Scope
- Implement only the objective for this subbundle.
- Preserve existing runtime behavior.
- Keep all side effects out of Core and driver verifier packages.
- Update tests and architecture guardrails when public surfaces change.

## Dependency Impact
- Downstream subbundle: `SB039`.
- If this subbundle changes public contracts or policy semantics, downstream verifier/adapter tests become untrustworthy until this subbundle is re-closed.

## Validation Depth
Focused source/test proof is sufficient, but must be compatible with the next critical gate.

## Implementation Steps
1. Re-read exact source references.
2. Make the smallest coherent production/test/doc changes that satisfy the objective.
3. Add or update failing-first/adversarial negative tests where behavior or contract surface changes.
4. Run focused tests for this area.
5. Update proof artifacts and execution report row.

## Scope Exceptions
- Do not implement generic runtime host or production execution-capable driver.
- Do not wire the verifier into scheduler, workflow, manager command, or DI.
- Do not add UI/media proof.

## Do Not Do
- Do not introduce `IProcessDriverRegistry`, runtime selector, DI extension, manager command, shell execution, Graph/Office call, workspace/storage write, process mutation, claim/transition/finalizer/retry mutation.
- Do not add broad Core runtime ownership.
- Do not accept non-empty output as proof.

## Acceptance Checklist
- [ ] Objective implemented.
- [ ] No forbidden runtime/Core/driver dependency drift.
- [ ] Tests include negative and positive cases where applicable.
- [ ] Source scans pass.
- [ ] Execution report row updated.
- [ ] Downstream dependencies checked.

## Proof Required
- Build/test/source proof relevant to this subbundle.
- Source assertions for all changed files.
- Anti-stub audit.


## Browser Validation Logging
N/A. This subbundle does not affect browser-visible or host-visible UI. If UI/media files change, fail and re-scope.

## Progression Gate
Must pass before the next subbundle starts.

## Suggested Agent Prompt
Implement `SB038` from `bundle://subbundles/SB038/README.md`. Preserve hard boundaries, update proof, and stop if implementation requires runtime host, DI, manager command, process mutation, storage/workspace writes, shell/Graph calls, or UI changes.
