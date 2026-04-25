# 05 e2e regression proof

## Status

- `Ready`

## Objective

Add final regression proof that the process service can run the deterministic multi-role calculator process end to end with mock agents and no real LLM calls.

## Covered Inputs

- REQ-007: settings-gated mock execution.
- REQ-010: true E2E process run.
- REQ-011: actionable process failure diagnostics.

## Prerequisites

- Subbundle 01 progression gate must pass.
- Subbundle 02 progression gate must pass.
- Subbundle 03 progression gate must pass.
- Subbundle 04 progression gate must pass.

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessOutboxIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessOutbox.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\TestApplication.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Support\TestApplicationBootstrap.cs`

## Deliverables

- One E2E integration test that:
  - enables `AgentFramework:ProcessMockAgents:Enabled`
  - seeds the mock catalog
  - creates or imports the deterministic calculator process
  - staffs it with mock agents
  - starts a run
  - drains process outbox automation dispatch until settled
  - verifies QA rejection, repair, QA approval, and release completion
  - asserts no real provider was called
  - asserts no process outbox records are dead-lettered
- Execution report updated with final status and exact proof commands.

## Dependency Impact

- This is the final closure subbundle.
- It proves the earlier fixes work together rather than only in isolated unit or integration tests.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Create the E2E test fixture using the stable lifecycle and calculator process model from earlier subbundles.
2. Drive dispatch through durable outbox processing, not manual step transitions.
3. Poll with bounded timeouts and clear diagnostics for pending, failed, blocked, or dead-letter state.
4. Assert ordered step states and selected branch outcomes.
5. Assert process artifacts exist for scope, architecture, implementation, QA finding, repair, QA approval, and release notes.
6. Assert AgentFramework execution records use `process-step` context and the mock provider/model.
7. Assert final process run status is completed.
8. Update execution report and close raw note rows only when all proof passes.

## Scope Exceptions

- No browser proof is required unless implementation changes Process Workspace UI.
- No real calculator app build is required unless the deterministic process model chooses to include build/test artifacts as required evidence.

## Do Not Do

- Do not manually transition steps to fake E2E completion.
- Do not use real provider credentials or network LLM calls.
- Do not hide dead-letter outbox records.
- Do not accept a run that skips the repair branch.

## Acceptance Checklist

- E2E test starts from process service APIs and completes through automation dispatch.
- QA first pass selects `repairs-required`.
- Repair developer runs after QA rejection.
- QA recheck selects `approved`.
- Release manager runs after QA approval.
- Run status is `Completed`.
- All required artifacts are persisted and linked.
- No real provider execution appears in run details.
- No dead-letter process outbox records remain.

## Proof Required

- New focused E2E integration test command.
- Focused mock runtime tests.
- Focused process outbox tests.
- Focused dispatcher tests from subbundle 04.

## Browser Validation Logging

- N/A unless implementation touches browser-visible Process Workspace UI.
- If UI is touched, record route, viewport, actions, screenshot path, and visual review result here before closure.

## Progression Gate

- Final closure may pass only when the deterministic mock-agent process run completes end to end and every raw note in `reviews/01-execution-report.md` is closed with proof.

## Suggested Agent Prompt

```text
Implement subbundle 05 only. Add the final E2E regression proving the deterministic calculator process completes through process service, durable outbox dispatch, mock AgentFramework execution, branch routing, artifact projection, and final run completion with no real LLM calls.
```
