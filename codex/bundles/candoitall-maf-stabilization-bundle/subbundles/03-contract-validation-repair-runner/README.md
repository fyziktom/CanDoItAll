# 03 - Contract Validation, Repair, and Typed Execution Runner

## Objective

Make typed contract validation a general execution invariant. The process dispatcher validates `ProcessStepOutcomeResult`, but the generic execution service should also validate whenever a machine-critical structured output contract is declared.

## Primary files to inspect


- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidation.cs`
- `src/CanDoItAll.AgentFramework.Models/OutputContracts/AgentOutputContracts.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OutputValidation.cs`
- Existing tests for output contracts, process dispatch, and execution runs.


## Required implementation tasks


1. Add `IAgentOutputValidatorRegistry` or equivalent.
2. Move/reuse process-step outcome validation outside the dispatcher so it can be used by the generic execution service.
3. Add validators for machine-critical DTO families:
   - `ProcessStepOutcomeResult`
   - `ProcessStatePatch`
   - `AgentStepResult<TPayload>` envelope rules
   - `CodeReviewResult`
   - `ArchitectureReviewResult`
   - `ImplementationPlanResult`
   - `TestPlanResult`
   - `ToolExecutionDecisionResult`
   - `HumanEscalationRequest`
4. Make `AgentFrameworkWorkspaceExecutionService` validate structured output before completing a machine-critical run as succeeded.
5. Add bounded repair/retry support if it is missing or incomplete:
   - Retry count configurable; default 1 or 2.
   - Repair prompt includes schema/contract, invalid raw output, validation errors.
   - Revalidate repaired output.
   - Never bypass policy/security validation.
6. Store raw output hash, validation errors, repair attempts, and final validation status in run detail/diagnostics.
7. Keep process-specific artifact/tool proof checks in the process dispatcher, but avoid duplicating JSON parsing logic.


## Required tests


Unit tests:
- Top-level primitive/list contract is rejected.
- Valid `ProcessStepOutcomeResult` passes.
- Missing reason fails.
- Completed status with unresolved next-action/user-follow-up fails.
- Failed/Blocked/WaitingApproval statuses require appropriate next actions or escalation details.
- Invalid branch outcome fails when explicit branch selection is required.
- `ProcessStatePatchValidator` rejects protected paths and invalid operations.
- Code/architecture/test plan validators reject inconsistent statuses.
- Repair is bounded and repaired output is revalidated.

Integration tests:
- Structured output present + invalid response -> run does not complete as succeeded.
- Structured output present + valid response -> run completes.
- Process dispatcher reuses the shared validator.


## Risks and constraints


- Avoid over-validating exploratory/free-form chat runs.
- Make validator severity and failure behavior configurable if some DTOs are advisory rather than critical.

