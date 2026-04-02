# 01-standard-ring-order-and-node-menu-contract

## Status

- `Completed`
- `Proof: dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter "ProjectStructureActionCatalogAdapterTests|ProjectStructureCanvasCatalogTests" => Passed (12/12 on 2026-04-01)`

## Objective

- Define a deterministic node-menu ordering contract so the first ring is stable across node types and the remaining actions flow into the surrounding hive intentionally.

## Covered Inputs

- `N003` Most-used items belong in the center or first circle.
- `N004` Clockwise first-ring order should be `Blocks`, `Assets`, `Tasks`, `Progress`, `Markers`, then the best node-specific slot.
- `N005` This standard composition should apply for all nodes.
- `N006` Remaining actions should be organized in the best way for that specific node.

## Prerequisites

- Prepared-stage validator passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureActionShortcuts.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureCanvasCatalogTests.cs`

## Deliverables

- A shared ordering strategy for node context menus that keeps the first ring stable.
- Deterministic placement of the sixth first-ring slot per node type when action sets differ.
- Intentional ordering of overflow actions outside the first ring.
- Focused automated proof for the new ordering rules.

## Dependency Impact

- `02-02-hive-geometry-and-submenu-packing` depends on this phase because the new honeycomb positions only matter if the most important actions are actually in the intended slots.
- Weak proof here would invalidate later browser screenshots because the spatial memory contract would be accidental rather than real.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Identify the existing node-menu action families that should own the first ring.
2. Reorder node-context actions and any shared quick-create structures so the most common actions land first deterministically.
3. Ensure the sixth first-ring slot is chosen explicitly for each node family instead of by incidental list order.
4. Update component tests to verify first-ring order and deterministic overflow ordering.

## Scope Exceptions

- Do not change runtime geometry or CSS in this phase.

## Do Not Do

- Do not hand-code positions in JavaScript before the action order is stable.
- Do not solve browser spacing complaints with catalog-only changes.

## Acceptance Checklist

- Representative node menus emit first-ring actions in the intended order.
- Overflow actions remain deterministic and discoverable.
- Automated tests fail if the first-ring contract regresses.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "ProjectStructureActionCatalogAdapterTests|ProjectStructureCanvasCatalogTests"`
- Execution-report note describing the stabilized first-ring contract and any intentional node-specific sixth-slot decisions.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Downstream geometry work may continue only after focused tests prove the first-ring ordering contract is explicit and deterministic.

## Suggested Agent Prompt

```text
Implement only subbundle 01 for the project-structure canvas hive context menu bundle.
Stabilize the node-menu first-ring ordering contract, keep common actions in the requested clockwise sequence, choose the sixth slot deterministically per node family, and prove it with focused component tests.
Do not change runtime geometry or CSS in this phase.
```
