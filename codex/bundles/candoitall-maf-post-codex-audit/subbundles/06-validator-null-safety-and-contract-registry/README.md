# Subbundle 06 — Validator Null-Safety and Contract Registry

## Goal

Make output validation robust against missing/null fields and register all stable critical contracts.

## Current problem

Some validators call `.Count` on required collections without null checks. Invalid model output can cause `NullReferenceException` instead of a structured validation result.

## Implementation tasks

1. Add exception safety in `AgentOutputJson.DeserializeAndValidate(...)`.

Wrap validator invocation:

```csharp
try
{
    var validation = validator.Validate(output);
    ...
}
catch (Exception ex)
{
    return Failure(rawOutput, AgentOutputValidationResult.Failure(new AgentOutputValidationError
    {
        Code = "agent.output.validator_exception",
        Message = ex.Message,
        Path = "$",
        Severity = AgentOutputValidationSeverity.Critical
    }), output);
}
```

Do not use this as a substitute for fixing validators; it is a safety net.

2. Fix validators.

Add null checks for:

- `CodeReviewResult.Findings`
- `CodeReviewResult.RequiredActions`
- `ArchitectureReviewResult.RequiredActions`
- `ArchitectureReviewResult.BoundaryConcerns`
- `ImplementationPlanResult.Tasks`
- each `ImplementationTask`
- `TestPlanResult.TestCases`
- any other collection/nested DTOs.

3. Add helper methods.

Suggested helper:

```csharp
private static bool RequireCollection<T>(
    IReadOnlyList<T>? value,
    string path,
    string code,
    string message,
    List<AgentOutputValidationError> errors)
```

4. Expand contract registry.

Register stable contracts for:

- `ProcessStepOutcomeResult`
- `CodeReviewResult`
- `ArchitectureReviewResult`
- `ImplementationPlanResult`
- `TestPlanResult`
- `ToolExecutionDecisionResult`
- `ProcessStatePatch`
- any envelope/wrapper used for machine-critical runs.

5. Tests.

Required tests:

- Missing collection property returns validation failure, not exception.

## Execution Result

Status: Complete. Validators now handle null/missing collections with structured validation errors, validator exceptions are converted to structured errors, and all critical contracts are registered.
- Explicit `null` collection returns validation failure, not exception.
- Null nested task returns validation failure.
- Validator exception is converted into `agent.output.validator_exception`.
- Every known contract key resolves.
- Every critical structured output used by execution has a registered validator.

## Acceptance gate

Invalid agent JSON must produce structured validation errors, not runtime validator exceptions.
