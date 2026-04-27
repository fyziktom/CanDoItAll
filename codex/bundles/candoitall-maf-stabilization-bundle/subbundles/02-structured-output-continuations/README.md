# 02 - Preserve Structured Output Across Approval and Background Continuations

## Objective

Fix continuation paths so machine-critical structured-output contracts are not dropped after approvals or background continuations. The current initial process run passes `StructuredOutput`, but some continuation paths pass `structuredOutput: null`.

## Primary files to inspect


- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/ExecutionCheckpointServices.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/ExecutionRunRequest` model definitions
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- Persistence models for execution run details/checkpoints.


## Required implementation tasks


1. Find every runtime call that passes `structuredOutput: null`.
2. Classify whether the path is truly free-form or should preserve an original structured-output contract.
3. Persist or reconstruct structured-output contract metadata for pending approvals and continuation runs.
4. Ensure manual approval continuation passes the original `AgentStructuredOutputContract` to `RespondToPendingApprovalsAsync(...)`.
5. Ensure auto-approved continuation after manual continuation also preserves the contract.
6. If a governed process step or machine-critical run cannot resolve its required contract, fail with a typed error instead of continuing as free-form text.
7. Preserve existing behavior for genuinely free-form chat runs.
8. Add diagnostics showing the structured-output contract name/type used for each continuation.


## Required tests


Unit tests:
- A pending approval checkpoint stores enough metadata to recover the structured-output contract.
- Manual approval continuation calls runtime with the contract restored.
- Auto-approved continuation path preserves the same contract.
- A governed process-step continuation without a resolvable contract fails clearly.

Integration tests:
- Simulate a process step that triggers a tool approval and then returns final `ProcessStepOutcomeResult` after approval.
- Invalid JSON after approval does not complete the step.
- Valid structured outcome after approval completes as expected.


## Risks and constraints


- Avoid serializing arbitrary `Type` objects directly if the persistence format cannot safely handle them. Store a stable contract key and map it to known contract types.
- Do not break free-form chat runs that intentionally have no structured-output contract.

