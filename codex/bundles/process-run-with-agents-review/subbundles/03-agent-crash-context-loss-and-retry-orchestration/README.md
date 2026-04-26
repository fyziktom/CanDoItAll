# 03 Agent Crash Context Loss And Retry Orchestration

## Status

- `Implemented and validated`

## Objective

Add explicit recovery orchestration for agent crash, interrupted execution, context loss, and blocked/failed agent-owned steps so the user can rerun the job with proper instructions and durable context.

## Covered Inputs

- REQ-003: Execution attempts and recovery classification.
- REQ-007: Structured recovery context for crash/context loss.
- REQ-008: Manual retry/rerun command.
- REQ-011: Strict governed completion remains intact.

## Prerequisites

- Subbundle 01 exposes attempt/health state.
- Subbundle 02 exposes missing artifact obligations.
- Existing dispatcher retry tests are passing before changes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunRecoveryWorker.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeViewModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.StepTransitions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RuntimeOperations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\AgentFrameworkExecutionRecoveryService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`

## Deliverables

- A recovery classification model for automatic retry, crash recovery, context reset retry, provider repair retry, and manual rerun.
- A durable recovery context package that summarizes prior attempt status, missing artifacts, failed tools, prior artifacts, branch requirements, and exact next-attempt instructions.
- A Process Workspace action to rerun an agent-owned blocked/failed step with an auditable reason and generated recovery directive.
- Backend command/service support for manual rerun without silently resetting process history.
- Tests for interrupted AgentFramework run recovery, failed step manual rerun, missing artifact rerun, and context reset behavior.

## Dependency Impact

- Depends on subbundles 01 and 02.
- Unlocks browser negative-path proof in subbundle 05.
- Incorrect implementation could duplicate work, overwrite artifacts, or complete a step without proof.

## Validation Depth

- Integration tests for recovery command and dispatcher interaction.
- Component tests for retry/rerun UI state and disabled/enabled rules.
- Negative tests proving terminal process statuses are not retried accidentally.
- Tests must cover old context not poisoning the next attempt.

## Implementation Steps

1. Define recovery classification and recovery package shape in the process runtime boundary.
2. Build recovery package content from latest execution detail, artifact ledger, step/run state, and dispatcher diagnostics.
3. Add a manual rerun command for eligible agent-owned steps.
4. Ensure rerun creates audit records and preserves previous attempts/artifacts.
5. Ensure rerun uses fresh chat or explicit context according to policy.
6. Add UI affordance with recovery directive preview or summary.
7. Add tests for crash/interrupted execution and context-loss retry.

## Do Not Do

- Do not simply transition `Failed` back to `Ready` without an audit trail and recovery directive.
- Do not delete prior execution runs or artifacts.
- Do not retry terminal completed/cancelled process runs.
- Do not make retries unlimited.
- Do not rely only on free-text logs for recovery state.

## Acceptance Checklist

- Failed or blocked agent-owned steps show whether rerun is available.
- Rerun creates a new attempt with proper context and visible audit history.
- Missing artifact obligations are included in the rerun instructions.
- Interrupted/cancelled AgentFramework runs are classified distinctly from normal failed validation.
- Previous artifacts and attempts remain visible.

## Proof Required

- Integration tests for recovery command and dispatcher rerun.
- Tests for AgentFramework interrupted run recovery interaction.
- Component tests for rerun UI.
- Updated execution report with recovery classifications tested.

## Closure Proof

- Added typed recovery classification and manual rerun request flow for eligible agent-owned blocked/failed steps.
- Manual rerun writes an audit journal entry, preserves prior attempts/artifacts, builds missing-artifact recovery instructions, and clears stale chat context for the next dispatch.
- Passed `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRuntimeOperatorReadModelTests"` with 3 tests.
- Passed `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessCanvasSelectionPanelTests"` with 5 tests.

## Browser Validation Logging

- Not required for this subbundle unless the rerun UI is substantial.
- Full browser proof belongs to subbundle 05.

## Progression Gate

- Subbundle 05 may proceed only after an agent-owned failed/blocked step can be rerun from UI-facing service paths with structured recovery instructions.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Add structured recovery classification, recovery packages, and a manual rerun workflow for agent-owned blocked or failed steps. Preserve prior attempts and artifacts; do not weaken governed completion.
```
