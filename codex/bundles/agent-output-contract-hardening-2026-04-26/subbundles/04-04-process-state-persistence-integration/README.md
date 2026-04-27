# 04-process-state-persistence-integration

## Status

- `Completed`

## Objective

Move governed process step completion and branch selection away from markdown/loose JSON comments and onto validated typed output before process state changes are persisted or events emitted.

## Covered Inputs

- Required concepts 6, 7, 8, 9, and 10.
- Regression tests that raw markdown cannot approve a workflow step and malformed JSON cannot be persisted as successful output.
- Bundle requirements R2, R4, R6, R7, R8, R9, and R10.

## Prerequisites

- Subbundle 01 audit complete.
- Subbundle 02 validators available.
- Subbundle 03 structured output request path available.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Execution.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ToolValidation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.GovernedOutcomes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.GovernedRules.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Process automation prompt updated to require typed structured output rather than `PROCESS_STEP_OUTCOME` markdown comments.
- Dispatch logic deserializes and validates typed process step outcomes before deriving status, reason, and branch outcome.
- Legacy markdown comments no longer act as authoritative workflow decisions.
- Process-state updates and events occur only after validated typed output or explicit typed failure.
- Regression tests for markdown-only approval rejection and malformed JSON failure/retry.

## Dependency Impact

- This is the user-visible safety fix. Any remaining markdown decision path here would violate the primary requirement even if the shared contracts exist.

## Validation Depth

- Process-critical closure.

## Implementation Steps

1. Replace the governed outcome prompt instruction with structured-output semantics.
2. Add or wire a typed process outcome extractor using the shared validator.
3. Update branch outcome resolution to use typed fields only.
4. Ensure invalid typed output participates in existing retry/failure handling without being silently accepted.
5. Update integration tests that previously relied on `PROCESS_STEP_OUTCOME`.

## Scope Exceptions

- Existing display markdown may remain as secondary content if it is explicitly not a workflow source of truth.
- Full redesign of process graph editing is out of scope unless required to prevent unsafe agent-driven mutation.

## Do Not Do

- Do not parse process status, approval, or branch selection from markdown.
- Do not persist raw agent text as a successful process decision.
- Do not overwrite whole process objects from agent output.
- Do not suppress malformed output errors.

## Acceptance Checklist

- A markdown-only `PROCESS_STEP_OUTCOME` response cannot complete a governed step.
- Valid structured process outcome can complete a governed step.
- Invalid structured process outcome retries or fails with validation details.
- Branch outcome IDs are selected only from validated typed keys/titles and candidate data.
- Tests cover the regression behavior.

## Proof Required

- Targeted process integration test command.
- Build evidence after process module changes.

## Browser Validation Logging

- N/A.

## Progression Gate

- Subbundle 05 may proceed only after process-state updates no longer depend on parsed markdown comments for governed decisions.

## Suggested Agent Prompt

```text
Implement only subbundle 04. Replace process outcome markdown parsing with validated typed structured output and update tests.
```
