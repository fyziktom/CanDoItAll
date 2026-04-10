# JS hotspot inventory and boundaries

## Status

- `Completed`

## Objective

- Inventory the broader CanvasLib JavaScript surface, identify the largest maintainability hotspots, and lock the lowest-risk execution seams for this bundle so the refactor stays behavior-preserving and provable.

## Covered Inputs

- `N006` search across the js files in canvas lib
- `N008` improve bundle with new subbundles and execute them
- `R008` inventory and justify executed hotspots
- `R009` split the largest verified workbench-runtime hotspots

## Prerequisites

- `03-browser-proof-and-closure`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibBodyAssets.razor`
- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json`

## Deliverables

- A recorded inventory of the largest CanvasLib JS hotspots
- An explicit execution-boundary decision for this bundle
- New implementation-ready subbundles for the selected workbench-runtime seams

## Dependency Impact

- Every later organization subbundle depends on this phase because a wrong boundary decision would create low-confidence proof and unnecessary load-order risk.

## Validation Depth

- `Repo analysis and bundle-readiness control`

## Implementation Steps

1. Inventory CanvasLib JS file sizes and hotspot concentrations.
2. Compare workbench and calendar candidates against available proof surfaces.
3. Select the lowest-risk workbench seams and extend the bundle plan.
4. Re-run the prepared-stage validator before implementation starts.

## Scope Exceptions

- Calendar runtime hotspots are recorded but not executed in this bundle.

## Do Not Do

- Do not start editing JS before the extended bundle passes prepared validation.
- Do not widen this bundle into unrelated calendar-runtime refactors.

## Acceptance Checklist

- The new hotspot analysis is written into bundle assets.
- The new subbundles are explicit about file ownership and proof expectations.
- The execution boundary stays inside the verified workbench runtime.

## Proof Required

- Repo inventory evidence in bundle analysis assets
- Prepared-stage bundle validation after the extension lands

## Browser Validation Logging

- `N/A`

## Progression Gate

- Downstream implementation may continue only after the extended bundle passes prepared validation and the chosen seams remain limited to the verified workbench runtime.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Inventory CanvasLib JS hotspots, lock the execution boundaries for this bundle, and do not edit runtime files yet.
```
