# 02-typed-output-contracts-and-validation

## Status

- `Completed`

## Objective

Introduce the shared typed result contracts and validation abstractions needed to make agent output machine-validated before workflow use.

## Covered Inputs

- Required concepts 1, 4, and 7.
- Test categories for DTO serialization, validator accept/reject behavior, wrapped list outputs, and process patch validation.
- Bundle requirements R1, R4, R7, R9, and R11.

## Prerequisites

- Subbundle 01 audit must identify the concrete consumers that need these contracts.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\CanDoItAll.AgentFramework.Models.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\CanDoItAll.AgentFramework.Core.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`

## Deliverables

- DTO families for agent step results, output envelopes, validation results/errors, repair/failure/escalation, process patches, and major result categories.
- Validation interfaces and concrete validators for process step outcomes and process patches.
- Enums for machine decisions instead of arbitrary status strings.
- Unit coverage for serialization and validation rules.

## Dependency Impact

- The structured runner and process dispatch integration depend on stable contracts here. Weak or nullable-heavy DTOs would recreate the same loose decision surface with different names.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add DTOs in the narrowest shared project that avoids circular references.
2. Add validator abstraction and common validation result types.
3. Add process-specific validators where process state rules are known.
4. Add serialization and validator tests.
5. Confirm DTO top-level shapes are objects, not arrays or primitives.

## Scope Exceptions

- This subbundle does not yet wire Microsoft Agent Framework `ResponseFormat`.
- This subbundle does not migrate every possible future domain agent result to active use if the repository has no concrete consumer yet.

## Do Not Do

- Do not create one giant weakly typed DTO.
- Do not use string status fields where an enum is appropriate.
- Do not add XML documentation comments.
- Do not add new dependencies unless the existing framework cannot support required validation.

## Acceptance Checklist

- DTOs compile with nullable required members.
- Validators reject missing required fields, invalid process patch paths, protected-field mutations, and inconsistent outcomes.
- Validators accept representative valid outputs.
- Tests prove top-level list-like results are wrapped in object DTOs.

## Proof Required

- `dotnet test` coverage for the new contract and validator tests, or targeted test command if the full suite is not yet practical during this phase.

## Browser Validation Logging

- N/A.

## Progression Gate

- Subbundle 03 may proceed only after typed contracts and validators exist and pass focused tests.

## Suggested Agent Prompt

```text
Implement only subbundle 02. Add typed contracts and validation tests without wiring runtime execution yet.
```
