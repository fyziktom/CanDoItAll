# 04 dispatcher completion contract

## Status

- `Completed`

## Objective

Make the process automation dispatcher complete deterministic mock-agent steps through explicit outcome, branch, artifact, and diagnostic contracts without weakening governed completion for real agents.

## Covered Inputs

- REQ-005: branch outcome marker compatibility.
- REQ-008: explicit mock evidence mapping.
- REQ-009: required process artifact persistence.
- REQ-011: actionable diagnostics for non-finishing processes.

## Prerequisites

- Subbundle 01 progression gate must pass.
- Subbundle 02 calculator process graph must be proven.
- Subbundle 03 mock staffing must be deterministic.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.GovernedOutcomes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentSupport.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`

## Deliverables

- Dispatcher handling that resolves mock `PROCESS_STEP_OUTCOME` comments to status and branch outcome IDs.
- Artifact projection rules or mock runtime outputs that satisfy the calculator process artifact expectations deterministically.
- Tests for missing branch outcome, missing required artifact, missing technical agent binding, and dead-letter/failed dispatch diagnostics.
- No global relaxation of required tool or governed outcome checks.

## Dependency Impact

- This subbundle unlocks E2E proof.
- If artifact projection or outcome parsing is wrong, the final run can hang, fail, or incorrectly complete.

## Validation Depth

- Process-critical closure.
- Dispatcher unit/integration proof plus negative-path diagnostics.

## Implementation Steps

1. Confirm the dispatcher receives mock execution details with enough data to identify provider, role, artifacts, and response text.
2. Decide whether the mock runtime should emit additional structured artifact metadata or whether dispatcher projection should explicitly recognize mock-managed artifacts.
3. Ensure required process artifacts are recorded with correct expectation IDs, kind, trust, sensitivity, title, and provenance.
4. Ensure branch outcome keys `repairs-required` and `approved` resolve through existing `ResolveSelectedBranchOutcomeId` logic.
5. Add tests for successful mock outcome completion.
6. Add negative tests for missing branch, missing artifact, missing technical binding, and missing governed outcome marker.
7. Confirm normal non-mock dispatcher behavior remains covered by existing tests.

## Scope Exceptions

- Do not implement the full E2E process run here; that belongs to subbundle 05.
- Do not add support for arbitrary fake tool calls unless they are represented as explicit mock provider evidence.

## Do Not Do

- Do not count `ToolCalls` integers as proof of actual required tool execution.
- Do not silently complete governed steps without `PROCESS_STEP_OUTCOME`.
- Do not create broad fallback artifact matching that can attach unrelated files to expectations.

## Acceptance Checklist

- Mock QA rejection selects `repairs-required`.
- Mock QA approval selects `approved`.
- Required artifact expectations are satisfied by deterministic process artifacts.
- Missing evidence produces specific failure messages.
- Existing strict dispatcher tests remain green after expectation updates from subbundle 01.

## Proof Required

- Focused `ProcessRunAutomationDispatchServiceTests`.
- Focused mock runtime tests.
- New or updated dispatcher tests covering mock artifact projection and branch outcomes.

## Browser Validation Logging

- N/A. Backend dispatcher/artifact behavior only.

## Progression Gate

- Subbundle 05 may proceed only after dispatcher tests prove mock roles can complete individual process steps with correct artifacts and branch outcomes.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Make dispatcher completion work with explicit deterministic mock-agent evidence, branch outcomes, and artifacts. Preserve strict governed completion for real agents and update the execution report with focused test proof.
```
