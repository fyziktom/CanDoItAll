# Runtime state-machine and transition-policy extraction

## Status

- `Completed`
- `2026-04-13`: `ProcessesService.TransitionStepAsync` now delegates guard validation, runtime progression, and run-status recomputation to focused helpers instead of inlining the full state-machine path, branch-outcome guard rules are explicitly covered, and the targeted build/integration/MCP proof passed.

## Objective

- Extract the runtime transition hotspot into smaller policy and planning services so runtime behavior becomes easier to test, review, and maintain without changing the public command surface.

## Covered Inputs

- `U003` Architecture, overloaded functions, and unit-testability concerns.
- `BRQ-010` Runtime state-machine extraction.
- `F006` Runtime orchestration hotspot.

## Prerequisites

- `08-publication-versioning-and-clone-engine-decomposition` passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.Helpers.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessStepRunTransitions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeViewModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RuntimeOperations.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessesToolsTests.cs

## Deliverables

- A smaller public runtime command surface backed by extracted internal policy/planner services.
- Separated logic for transition guard, branch validation, dependent activation, non-selected path resolution, and run-status recompute.
- Tests that target the extracted runtime behaviors more directly.

## Dependency Impact

- Read-side and UI phases depend on runtime behavior being clearer and easier to reason about.
- Gate C will reject a superficial extraction that merely moves code around without reducing concentration.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Identify the responsibility seams inside the current transition flow and extract them into focused internal services or policies.
2. Keep the public runtime commands stable where possible while delegating to the extracted internals.
3. Ensure the extracted services still respect the transaction/concurrency rules from earlier phases.
4. Add or update tests so the extracted behaviors are directly covered without relying only on one large integration path.

## Scope Exceptions

- This phase does not redesign the overall runtime product scope.
- This phase does not yet optimize read-side query shapes.

## Do Not Do

- Do not move domain logic into the UI or MCP layer.
- Do not create a differently named runtime god service.
- Do not lose branch, journal, or improvement side effects during the extraction.

## Acceptance Checklist

- The main runtime orchestration method is materially smaller or thinner.
- Extracted policy/planner responsibilities are explicit and testable.
- Selected branch routing and non-selected path handling still behave deterministically.
- Existing runtime callers remain compatible.

## Proof Required

- Integration tests for runtime transition behavior.
- Any new unit tests for extracted policy/planner services.
- Execution-report notes describing the extracted runtime seams.

## Browser Validation Logging

- N/A for this phase unless runtime UI wiring must change visibly.
- If UI wiring changes, keep final browser closure for subbundle 13 or 16.

## Progression Gate

- Runtime transition behavior is preserved, the orchestration hotspot is materially decomposed, and the new seams are testable enough for read-side and UI work to build on confidently.

## Suggested Agent Prompt

```text
Implement only subbundle 09. Extract the runtime transition hotspot into smaller policy and planning services while preserving the public command surface. Keep branch and journal behavior stable, add tests for the extracted seams, and stop before read-side optimization or UI decomposition.
```
