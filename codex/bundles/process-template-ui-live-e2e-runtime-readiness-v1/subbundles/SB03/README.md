# SB03: Blazor/.NET automation hardening

## Status
Prepared.

## Objective
Strengthen the Blazor/.NET representative automation proof so it fully exercises dispatch/finalizer/artifact/readback and is not confused with the older manual-transition contract test.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime*.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch

## Deliverables
- Ensure the Blazor automation test verifies outbox records, execution runs, finalizer invocations, artifact records, selected branch outcome, and managed output readback.
- Add explicit assertion that the automation test does not use `SuppressAutomationDispatch = true`.
- Keep the older manual test only as a contract test and label it clearly in test name or comments.
- Add negative proof that missing process-mock role mapping fails before dispatch.

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

## Browser Validation Logging
N/A unless UI route changed.

## Progression Gate
SB04 may proceed only after Blazor automation E2E is production-path, not manual-transition proof.
