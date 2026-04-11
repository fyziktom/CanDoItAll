# 01-node-inventory-and-port-semantics

## Status

- `Ready`

## Objective

- Introduce a strongly-typed process-canvas node and port catalog that matches `architecture/02-node-port-matrix.md`, replaces ad hoc branch-only semantics as the process foundation, and makes later persistence and UI work implementable without guesswork.

## Covered Inputs

- `R001` Inventory every current process-canvas node family and its ports.
- `R002` Classify each editable connection family by cardinality.
- `R003` Keep the node and port model strongly typed.
- `R004` Capture step-kind applicability rules.

## Prerequisites

- Bundle preparation and prepared-stage validation must be complete.
- `- none` for earlier implementation subbundles because this is the first execution phase.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasBranching.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle\architecture\02-node-port-matrix.md`

## Deliverables

- A typed process-canvas port inventory in the process module.
- Explicit node-family and step-kind applicability rules for process nodes.
- Tests or equivalent assertions that the implemented inventory matches the documented matrix.
- Removal or consolidation of branch-only stringly port semantics where the typed catalog now covers them.

## Dependency Impact

- Every later subbundle depends on this contract being correct.
- If this phase is weak, later persistence and UI changes will encode the wrong semantics and downstream proof will be untrustworthy.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Extract the documented node families, port families, cardinality rules, and step-kind applicability into a typed process-canvas catalog.
2. Refactor the process module to reference the typed catalog instead of scattered branch-only string comparisons where the new catalog applies.
3. Add focused tests or assertions proving the catalog covers the documented node families and rules.
4. Update any bundle analysis notes if live repo reality differs materially from the prepared matrix.

## Scope Exceptions

- This phase does not yet change visible browser behavior.
- This phase does not yet extend database schema.

## Do Not Do

- Do not invent new business relationships beyond the documented matrix.
- Do not push artifact-link persistence into UI-only state as a shortcut.
- Do not skip step-kind applicability rules just because the current renderer ignores them.

## Acceptance Checklist

- A single authoritative process-canvas port catalog exists.
- The catalog covers role, step, branch-router, and runtime node families called out in the bundle.
- Step-kind applicability rules are explicit for at least `Start`, `Decision`, and `End`.
- Later phases can reference the catalog instead of re-deriving semantics locally.

## Proof Required

- Focused test command covering the new typed catalog or equivalent assertions.
- Code inspection showing the process module now has a single semantic source of truth for process-canvas ports.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Downstream subbundles may continue only after the typed process-canvas port catalog is in code, focused tests pass, and the implemented catalog still matches `architecture/02-node-port-matrix.md`.

## Suggested Agent Prompt

```text
Implement only subbundle 01 from C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle. Introduce a strongly-typed process-canvas node and port catalog that matches the documented node-port matrix, encode step-kind applicability rules, replace ad hoc branch-only semantics where the new catalog applies, add focused tests, and do not touch browser proof or schema work yet.
```
