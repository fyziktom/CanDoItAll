# 02 Definition Recomposition Tuning

## Status

- `Completed`

## Objective

- Tune automatic process definition recomposition so the canvas lays out a clear main path, branch lanes, nearby role nodes, and more readable spacing.

## Success Criteria

- Default-route dependencies stay on the primary step lane where possible.
- Custom and exception branch dependencies fan to side lanes.
- Role nodes are anchored near related steps by actual role assignment or decision-authority links.
- Collision cleanup preserves the composed step spine.
- Targeted component tests pass.

## Covered Inputs

- `N001` through `N007`
- `REQ-001` through `REQ-005`

## Prerequisites

- `01-layout-analysis-and-contract` completed.
- Prepared-stage bundle validator passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasRecompositionService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasBranching.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasRecompositionServiceTests.cs`

## Deliverables

- Revised recomposition constants and staged collision strategy.
- Semantic default-route lane handling.
- Role anchoring based on related step coordinates.
- Focused test coverage for main path, branch, spacing, and role-placement behavior.

## Dependency Impact

- `03-validation-and-browser-proof` depends on this phase. Browser proof cannot close the raw request if component-level geometry still treats default route as a side branch or leaves roles far from related steps.

## Validation Depth

- Critical UI foundation.

## Implementation Steps

1. Update step spacing and lane spacing constants conservatively.
2. Add semantic default-route detection to step lane assignment.
3. Replace GUID-based primary-child selection with route-aware and process-order-aware selection.
4. Anchor role nodes from related step coordinates.
5. Resolve role and branch collisions against pinned steps so the main path remains stable.
6. Update targeted tests.

## Scope Exceptions

- Browser proof is owned by subbundle `03`.
- WebGL layout modes are not redesigned in this phase.

## Do Not Do

- Do not modify process persistence or runtime execution semantics.
- Do not add external layout libraries.
- Do not change the toolbar UX beyond generated positions.

## Acceptance Checklist

- Existing collision and spacing tests still pass.
- Branching layout test asserts default path, branch router, branch lane, and role anchor behavior.
- Cycle rejection remains unchanged.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProcessCanvasRecompositionServiceTests`
- Broader build or test command if targeted tests expose compile errors outside the test project.

## Browser Validation Logging

- Browser-visible effect exists, but browser proof is deferred to `03-validation-and-browser-proof`.

## Progression Gate

- Passed. Targeted component tests passed after recomposition tuning and the execution report records the code-change proof.

## Completion Notes

- `ProcessCanvasRecompositionService` now chooses layout parents from primary continuation dependencies for multi-input steps, preserves default-route lanes, uses wider step and lane spacing, anchors roles near related step coordinates, and resolves roles/routers against pinned step boxes.
- `ProcessCanvasRecompositionServiceTests` now covers no-overlap recomposition, default-route lane behavior, role placement, branch separation, cyclic graph rejection, and the multi-dependency primary-continuation regression.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
