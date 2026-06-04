# SB06 Complexity / Size Inventory

## Before

The process dispatch service kept required-tool resolution, browser proof expectations, artifact validation, completion status, and retry decisions as private methods spread across the same partial class set. The behavior was difficult to address directly in tests without invoking broader dispatch flows.

## After

The extraction keeps the partial-class ownership boundary but introduces typed nested service members:

- `IRequiredToolResolver`
- `IBrowserProofRequirementResolver`
- `IArtifactRequirementMatcher`
- `IStepCompletionPolicy`
- `IDispatchDecisionEngine`

This is deliberately not a public DI abstraction. It creates testable and named decision points while avoiding a wider dependency churn.
