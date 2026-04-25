# 02 Artifact Contract And Missing Artifact Recovery

## Status

- `Implemented and validated`

## Objective

Make required process artifacts explicit, auditable, and recoverable by showing expectation satisfaction in UI and defining predictable behavior when an agent does not deliver an artifact.

## Covered Inputs

- REQ-005: Artifact obligation ledger.
- REQ-006: Missing artifact behavior.
- REQ-011: Strict governed completion remains intact.
- REQ-012: Existing component patterns are preserved.

## Prerequisites

- Subbundle 01 run/step health UI is complete.
- Existing process artifact projection tests are understood.
- The deterministic process mock path remains green before changes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.Operations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeViewModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsArtifactsSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsExecutionSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- A per-step artifact expectation satisfaction read model with statuses such as expected, satisfied, auto-projected, missing, projection failed, and not applicable.
- Evidence UI that maps each required expectation to the execution artifact or process artifact record that satisfied it.
- UI diagnostics for required artifact names that were missing after an agent attempt.
- Backend tests for no-artifact delivery, missing file path, unreadable file, projection mismatch, and response-text-only projection.
- Recovery policy documentation in code/tests: retry while attempts remain, block with explicit missing artifact state after attempts are exhausted, fail on projection exceptions that make state unsafe.

## Dependency Impact

- Unlocks subbundle 03 because recovery directives must include missing artifact obligations.
- Unlocks subbundle 05 because browser proof must inspect produced and missing artifacts.

## Validation Depth

- Dispatcher helper/unit tests for missing required artifact classification.
- Integration tests for process artifact records and expectation IDs.
- Component tests for artifact ledger rendering.
- Negative tests must prove missing required artifacts do not silently complete the step.

## Implementation Steps

1. Model artifact expectation satisfaction in the runtime read query.
2. Preserve existing artifact records while adding expectation status and source detail.
3. Surface missing/projection-failed artifacts in Evidence and selected-step views.
4. Ensure transition reasons and blocked reasons stay concise but link to the richer ledger.
5. Add deterministic tests for agents that omit, misplace, or misclassify artifacts.
6. Update prompts/recovery directive inputs so missing artifact names can be reused by subbundle 03.

## Do Not Do

- Do not count arbitrary produced files as satisfying required process artifacts.
- Do not hide missing required artifacts by auto-recording vague response summaries.
- Do not relax mock artifact matching to broad title/path guesses.
- Do not implement manual rerun controls here; expose the data needed for them.

## Acceptance Checklist

- Every required expectation on a selected step has a visible status.
- Missing required artifact state is visible before and after the step becomes blocked.
- Produced AgentFramework artifacts and projected process artifacts are distinguishable.
- Required artifacts missing after max attempts keep the step non-completed.
- Tests prove both satisfied and unsatisfied artifact paths.

## Proof Required

- Focused dispatcher tests for missing required artifacts.
- Focused process runtime read query tests for artifact ledger projection.
- Focused component tests for artifact ledger display.
- Existing deterministic mock E2E still passes.

## Closure Proof

- Added artifact expectation satisfaction read models and UI ledgers for missing/satisfied process artifact obligations.
- Missing required artifacts remain non-completing governed state and appear in recovery context.
- Passed `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRuntimeOperatorReadModelTests"` with 3 tests.
- Passed `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessOutboxIntegrationTests"` with 137 tests.

## Browser Validation Logging

- Not required for closure unless the Evidence tab layout changes substantially.
- Browser artifact inspection is required later in subbundle 05.

## Progression Gate

- Subbundle 03 may proceed only when missing artifact obligations are available as structured data for recovery directives and UI.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Add a structured artifact expectation satisfaction ledger and UI diagnostics for missing required artifacts. Preserve strict process completion rules and do not add manual rerun controls yet.
```
