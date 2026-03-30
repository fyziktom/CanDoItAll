# Asset topology and duplicate retirement

## Status

- `Completed`

## Objective

- Establish CanvasLib as the only active shipped owner of the workbench and calendar asset surface, and remove or disable the duplicate `ComponentKit` static asset publish path that currently creates the third `canvasWorkbenchInterop.js` copy.

## Covered Inputs

- `N001 Keep only the CanvasLib canvasWorkbenchInterop owner`
- `N003 Split CanvasLib wwwroot JS and CSS resources into folders`
- `R01`

## Prerequisites

- `none`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\CanDoItAll.ComponentKit.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibHeadAssets.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibBodyAssets.razor`
- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json`

## Deliverables

- A verified duplicate-retirement decision documented in code and bundle status.
- `ComponentKit` no longer publishes the duplicate CanvasLib asset set, or an explicitly justified narrower alternative if the project file proves that only selective exclusion is safe.
- Source and bundle evidence that CanvasLib is the active runtime asset owner.

## Dependency Impact

- Subbundles `02`, `03`, and `04` all depend on this phase.
- If this phase is weak, later browser or test proof may accidentally validate the wrong static asset source and make the whole execution untrustworthy.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Reconfirm that active source consumers in `src\` and `tests\` do not reference `_content/CanDoItAll.ComponentKit/...`.
2. Update `ComponentKit` so it no longer publishes the duplicated CanvasLib `wwwroot` asset set, or narrow the publish set to only the files that are still truly owned there.
3. Rebuild the relevant projects and confirm the static-web-asset graph no longer exposes the duplicate CanvasLib runtime path through `ComponentKit`.
4. Record the outcome in the execution report before allowing any split work to start.

## Scope Exceptions

- `none`

## Do Not Do

- Do not refactor `ComponentKit` runtime behavior beyond the duplicate asset ownership problem.
- Do not start splitting CanvasLib files yet.
- Do not keep both publish paths alive “just in case” without hard evidence.

## Acceptance Checklist

- Source-only audit shows no active consumer dependency on `ComponentKit` asset URLs.
- `ComponentKit` no longer ships the duplicate CanvasLib `canvasWorkbenchInterop.js` asset as an active publish copy.
- The bundle documents the ownership decision and resulting proof.

## Proof Required

- A source-only search result for `_content/CanDoItAll.ComponentKit/...` across `src\` and `tests\`.
- A build or asset-graph proof showing the duplicate publish path was removed or narrowed.
- Browser evidence on structure and calendar routes that the app runs without depending on `ComponentKit` asset URLs.

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`
- Route: `/projects/{projectId}/calendar`
- Viewport: `1600x900`
- Required actions: navigate after the build, inspect network/static asset origin, confirm no runtime load of `_content/CanDoItAll.ComponentKit/...`
- Screenshot paths: `output/playwright/canvaslib-asset-origin-structure.png`, `output/playwright/canvaslib-asset-origin-calendar.png`
- Review questions:
  - Are the routes functional after duplicate retirement?
  - Is there any runtime evidence that `ComponentKit` is still serving the CanvasLib asset surface?

## Progression Gate

- Downstream work may continue only after CanvasLib is proven to be the sole active owner of the shipped workbench runtime asset surface.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Retire the duplicate ComponentKit static asset ownership path without widening into unrelated runtime refactors, then prove CanvasLib is the only active shipped owner before any file splitting starts.
```
