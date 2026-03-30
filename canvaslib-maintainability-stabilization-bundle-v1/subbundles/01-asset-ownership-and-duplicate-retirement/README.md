# Asset ownership and duplicate retirement

## Status

- `Completed`

## Objective

- Leave the active repo surface with one canonical CanvasLib asset tree, retire redundant mirror copies, and close the legacy duplicate-project decision with evidence instead of assumption.

## Covered Inputs

- `N001 one valid copy of folders/files in repo`
- `N002 analyze other parts of the repo for potential duplicities like this`
- `R001 Canonical CanvasLib Asset Ownership`
- `R002 Duplicate Audit Beyond CanvasLib`

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js`
- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json`
- `C:\repositories\CanDoItAll\tools\canvaslib\build-assets.cjs`
- `C:\repositories\CanDoItAll\tools\canvaslib\verify-assets.cjs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibHeadAssets.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibBodyAssets.razor`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\src`

## Deliverables

- One canonical CanvasLib asset tree in source control for the active public asset surface
- Updated asset tooling that no longer requires identical mirror trees
- Repo duplicate inventory updated in the execution report
- `CanDoItAll.ComponentKit` either retired or explicitly recorded as a validated exception

## Dependency Impact

- Every later subbundle depends on the canonical asset layout. If this phase is wrong, browser proof in later phases can fail for reasons unrelated to the C# refactors and make closure evidence untrustworthy.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Audit the existing CanvasLib asset mirror and choose the canonical tree that will remain in source control.
2. Update the manifest and asset scripts so they operate on one canonical asset copy while preserving generated include components and public asset URLs.
3. Remove the redundant CanvasLib mirror tree from source control.
4. Re-check the repo for similar duplicate patterns and verify whether `CanDoItAll.ComponentKit` is still active.
5. If `CanDoItAll.ComponentKit` is unreferenced, retire it as the legacy duplicate canvas surface.
6. Capture the duplicate audit and browser-loading proof before allowing downstream refactors.

## Scope Exceptions

- If a hidden `ComponentKit` consumer appears during execution, do not retire it blindly. Record the exact consumer and keep the exception open for the execution report.

## Do Not Do

- Do not rewrite workbench runtime behavior.
- Do not change `_content/CanDoItAll.Components.CanvasLib/...` URLs.
- Do not move CanvasLib C# component or graph files in this phase unless the asset cleanup requires a trivial adjacent fix.

## Acceptance Checklist

- CanvasLib `wwwroot` no longer contains parallel identical `css` and `css-src` trees or parallel identical `js` and `js-src` trees.
- Asset tooling and generated include components still pass.
- Shared canvas routes load without missing-script or missing-style regressions.
- The execution report records the repo duplicate decision for `CanDoItAll.ComponentKit`.

## Proof Required

- `npm run canvaslib:build-assets`
- `npm run canvaslib:verify-assets`
- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- Duplicate audit command proving the CanvasLib mirror tree is gone
- Active-solution audit proving whether `CanDoItAll.ComponentKit` is referenced
- Browser proof that the shared canvas routes still load the cleaned asset graph

## Browser Validation Logging

- Routes:
  - `/projects/{projectId}/structure`
  - `/projects/{projectId}/calendar`
  - `/prompt-factory`
- Viewports:
  - `1900x1200`
  - `1600x900`
- Required Playwright proof:
  - navigate to each route
  - verify the page finishes loading without missing static asset failures
  - capture at least one shared-canvas screenshot after the asset cleanup
- Screenshot review:
  - no unstyled shell
  - no missing toolbar or overlay chrome
  - no console-visible static-asset failures

## Progression Gate

- Downstream work may continue only after the asset commands pass, the web build passes, the browser route smoke passes, and the duplicate inventory is updated with the `ComponentKit` decision.

## Suggested Agent Prompt

```text
Implement only the asset ownership and duplicate retirement phase.
Keep one canonical CanvasLib asset tree, preserve public asset URLs, update the tooling accordingly, and close the duplicate audit with explicit evidence.
```
