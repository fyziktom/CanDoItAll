# SB03: Blazor/.NET automation hardening

## Status
- Status: Completed

## Objective
Strengthen the Blazor/.NET representative automation proof so it fully exercises dispatch/finalizer/artifact/readback and is not confused with the older manual-transition contract test.

## Covered Inputs
- Raw request: determine whether representative templates work again like before.
- REQ-003: prove Blazor/.NET template automation through dispatch, finalizer, artifacts, and run detail readback.

## Prerequisites
- SB01 baseline gate passed.
- SB02 either proved the user-visible launch path or recorded a concrete UI blocker that does not invalidate backend automation hardening.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.SessionState.cs
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.PromptArtifacts.cs
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.BranchOutcomes.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch

## Deliverables
- Ensure the Blazor automation test verifies outbox records, execution runs, finalizer invocations, artifact records, selected branch outcome, and managed output readback.
- Add explicit assertion that the automation test does not use `SuppressAutomationDispatch = true`.
- Keep the older manual test only as a contract test and label it clearly in test name or comments.
- Add negative proof that missing process-mock role mapping fails before dispatch.

## Dependency Impact
- SB04 and SB05 depend on this subbundle proving automation dispatch is production-path rather than manual transition proof.
- Runtime-host and scheduler proof in SB06-SB07 may cite execution-run and artifact readback patterns established here.

## Validation Depth
- Run focused integration tests for Blazor/.NET automation positive and missing-role negative cases.
- Scan for `SuppressAutomationDispatch = true` on the automation proof path.
- Include semantic adequacy proof, manifest, failing-first or adversarial transcript, passing transcript, source assertions, and anti-stub audit under `proof/SB03/`.

## Implementation Steps
- Audit existing Blazor/.NET E2E tests and identify any manual-transition proof path.
- Strengthen assertions for outbox completion, execution-run mapping, finalizer invocation, artifact persistence, selected branch outcome, and managed readback.
- Add missing process-mock role mapping negative proof before dispatch.
- Capture focused transcripts and source scans.

## Do Not Do
- Do not use manual step transitions as E2E automation proof.
- Do not add Blazor/.NET concepts to Process Core.
- Do not hide failures by increasing timeouts without diagnostics.

## Acceptance Checklist
- Blazor automation E2E passes through process-mock agents.
- Outbox records are completed.
- Execution runs map to all automated steps.
- Required artifacts exist and are managed-storage-backed.

## Proof Required
- Focused integration test transcript.
- Source scan for `SuppressAutomationDispatch = true` not appearing in automation proof path.
- Failure diagnostics on dead-lettered or timed-out outbox records.

## Proof Captured
- Manifest: `bundle://proof/SB03/manifest.md`
- Semantic invariants: `bundle://proof/SB03/semantic-invariants.md`
- Focused integration transcript: `bundle://proof/SB03/transcripts/focused-integration.txt`
- Source assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Suppression scan: `bundle://proof/SB03/transcripts/suppress-dispatch-scan.txt`
- Code-first guard: `bundle://proof/SB03/transcripts/code-first-guard.txt`
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Failing-first baseline: `bundle://proof/SB03/transcripts/failing-first-source-assertion.txt`

## Browser Validation Logging
- N/A unless this subbundle changes a browser-visible route; if it does, add Playwright evidence and screenshots to the execution report.

## Progression Gate
- SB04 may proceed only after Blazor automation E2E is production-path, not manual-transition proof.
- Reopen SB03 if a later representative template test relies on suppressed dispatch or manual transitions.

## Suggested Agent Prompt
- Implement SB03 by hardening the Blazor/.NET automation E2E around production dispatch, finalizer, artifact, branch, and readback behavior. Add negative missing-role proof and artifact-backed semantic evidence under `proof/SB03/`.
