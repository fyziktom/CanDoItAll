# SB017 - Manager UI large-screen readback

## Status
Prepared.

## Objective
Add a small run-detail/operator panel or verified existing surface for host status/audit diagnostics on large desktop.

## Covered Inputs
- Raw request: prefer code-heavy implementation over proof-only bundle churn.
- Runtime-host roadmap: move toward generic process driver runtime host without approving execution-capable drivers prematurely.

## Prerequisites
Previous critical gate must pass before this subbundle starts.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostModels.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostOptions.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs

## Deliverables / Scope
UI code allowed and expected.

## Dependency Impact
Downstream phases rely on this subbundle not weakening Process Core genericity, no-mutation guarantees, and exact lane selection.

## Validation Depth
Playwright large desktop proof.

## Implementation Steps
1. Inspect the exact source references before editing.
2. Make the minimum coherent source/test changes required by the objective.
3. Prefer production/test code over new proof artefacts.
4. Record concise proof only after source/test changes are complete.
5. Update docs only if operator behavior or approval status changed.

## Scope Exceptions
Execution-capable drivers remain out of scope. Do not implement effectful driver execution.

## Do Not Do
- Do not add reflection discovery or fallback selector.
- Do not add `object`/dynamic payload dispatch.
- Do not add shell/package restore/Graph/CRM/workspace/storage/process mutations.
- Do not add concrete `codex/bundles/<name>` paths into source or tests.
- Do not generate large proof-only artefact trees.

## Acceptance Checklist
- [ ] Source/test changes are material for this subbundle.
- [ ] No mutation flags remain false.
- [ ] No Process Core dependency drift.
- [ ] No bundle-path coupling in src/tests.
- [ ] Validation command transcript recorded.
- [ ] Code-vs-bundle diff stats updated at the next critical gate.

## Proof Required
Concise status row and link to the next critical gate proof. Do not create full manifest unless this subbundle unexpectedly changes a production signal.

## Browser Validation Logging
Large-screen Playwright required if UI/operator route changed.

## Progression Gate
May proceed after focused validation passes; critical proof is consolidated at the next gate.

## Suggested Agent Prompt
Implement SB017 as a code-first slice. Do not close with report-only proof. Make source/test changes first, then record concise evidence.
