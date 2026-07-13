# process-contract-and-template-repair

## Status

- `Completed`

## Objective

- Repair .NET multiteam process templates so subprocess launch, architecture planning, implementation, repair, and QA validation have explicit, correct operation contracts.

## Success Criteria

- Architect steps are read-only and cannot mutate product files.
- Subprocess launcher steps have launch/external-action authority and do not masquerade as code mutation steps.
- Product implementation and repair steps have mutable product scope.
- Template projection tests cover the corrected invariants.

## Covered Inputs

- R2, R3, R5.

## Prerequisites

- SB01 progression gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\dotnet-development-slice\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\dotnet-feature-function-implementation\definition.json`
- `C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit\ProcessDefinitionCatalogProjectionTests.cs`

## Deliverables

- Template JSON and step-doc changes.
- Focused template projection tests.

## Dependency Impact

- SB04 requires these templates to be corrected before restarting 5032 and launching a fresh process run.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Inspect the exact step definitions and prompts for the failing path.
2. Update only the contracts and prompt language needed to separate launch, architecture, implementation, repair, and validation.
3. Add/adjust unit tests for the corrected contracts.
4. Run targeted tests.

## Scope Exceptions

- Do not redesign unrelated non-.NET process templates unless tests reveal the same defect in shared code.

## Do Not Do

- Do not grant architects product mutation.
- Do not collapse the multiteam process into one monolithic code step.

## Acceptance Checklist

- Template tests fail before the fix and pass after the fix.
- The corrected contracts are explicit in JSON and docs.
- No unrelated template churn.

## Proof Required

- Targeted test output.
- Diff summary in `reviews/01-execution-report.md`.

## Browser Validation Logging

- N/A.

## Progression Gate

- SB04 remains blocked until template projection tests pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
