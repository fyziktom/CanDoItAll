# hr-readiness-capability-guardrails

## Status

- `Completed`

## Objective

- Make HR/readiness detect missing operation/tool capabilities for process steps before execution creates a false escalation loop.

## Success Criteria

- Readiness rejects or flags under-capable assignments with actionable diagnostics.
- Tests cover missing product mutation, missing subprocess launch authority, and missing validation/runtime proof capabilities where those are semantically required.
- Existing valid assignments still pass.

## Covered Inputs

- R4.

## Prerequisites

- SB01 progression gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\Services\ProcessRuntimeIntegrationServices.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Processes.cs`
- `C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs`
- `C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs`

## Deliverables

- Runtime/readiness guardrail code or tests proving existing code already enforces the corrected contracts.
- Actionable diagnostics for missing required capabilities.

## Dependency Impact

- SB04 requires readiness to reject bad assignments before launching the real run.

## Validation Depth

- Critical runtime guardrail.

## Implementation Steps

1. Inspect launch executor and runtime readiness logic.
2. Add the smallest semantic/contract check needed to catch the current defect.
3. Add focused tests for rejection and successful matching.
4. Run targeted resolver/runtime tests.

## Scope Exceptions

- Do not build a new HR system or broad role ontology.

## Do Not Do

- Do not silently add fallback tools to agents.
- Do not make readiness pass by ignoring step operation contracts.

## Acceptance Checklist

- Missing capabilities are named in the failure message.
- Valid existing assignments keep passing.
- Tests show HR/readiness would not have accepted the failing path silently.

## Proof Required

- Targeted test output.
- Before/after readiness behavior summary in `reviews/01-execution-report.md`.

## Browser Validation Logging

- N/A.

## Progression Gate

- SB04 remains blocked until readiness tests pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
