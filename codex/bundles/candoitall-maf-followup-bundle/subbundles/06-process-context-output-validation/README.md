# Subbundle 06 — Process-context output validation

## Problem

`ProcessStepOutcomeValidator` validates generic shape and basic consistency, while process-specific rules such as branch selection are checked later in the dispatcher. That can be OK, but the separation must be explicit and tested. Governed runs should never accidentally complete by falling back to text/Markdown heuristics.

## Current good point

`CanImplicitlyCompleteGovernedStep(...)` currently returns false, which prevents governed runs from completing merely because execution artifacts look successful without a valid `ProcessStepOutcomeResult`.

## Required change

Introduce or document a process-context validator for `ProcessStepOutcomeResult` that runs after generic DTO validation and before process state transition decisions.

Possible shape:

```csharp
public interface IProcessStepOutcomeContextValidator
{
    AgentOutputValidationResult Validate(
        DispatchCandidate candidate,
        ProcessStepOutcomeResult outcome,
        ExecutionRunDetail detail);
}
```

Use internal types as appropriate; do not over-generalize.

## Rules to test

- If a step requires explicit branch outcome selection, `Completed` must include a valid `BranchOutcomeKey` or valid `BranchOutcomeTitle`.
- Invalid branch key/title fails before completion.
- `Completed` with missing required implementation proof becomes blocked/failed according to current process rules.
- `Completed` with missing required artifacts becomes blocked/failed according to current process rules.
- `Failed`, `Blocked`, and `WaitingApproval` require actionable `NextActions`.
- `Completed` should require evidence references when the step has evidence expectations or generated artifacts. If this is too strict for all steps, define the exact condition.
- Markdown summary must never determine branch or completion status.

## Tests

Add integration or unit tests around `ProcessRunAutomationDispatchService` with fake execution details/outcomes. Prefer small tests that directly verify process-context validation rather than large live-agent tests.
