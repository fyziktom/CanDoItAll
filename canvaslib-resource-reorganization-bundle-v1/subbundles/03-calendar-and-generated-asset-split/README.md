# Calendar and generated asset split

## Status

- `Completed`

## Objective

- Split the CanvasLib calendar monolith into logical source folders and smaller public output files, then prove the calendar route still works under the regenerated asset graph.

## Covered Inputs

- `N003 Split CanvasLib wwwroot JS and CSS resources into folders`
- `R06`
- `R07`
- `R08`
- `R09`

## Prerequisites

- `subbundles/02-workbench-runtime-and-stylesheet-split` completed with a passed closure gate

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\calendar`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\calendar\core`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\calendar\controller`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\calendar\render`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\calendar`
- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json`
- `C:\repositories\CanDoItAll\tools\canvaslib\build-assets.cjs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibBodyAssets.razor`

## Deliverables

- A deeper calendar source tree under `wwwroot\js-src\calendar\**` by responsibility.
- Updated manifest and generated include components for the split calendar assets.
- Generated public calendar files that stay below 2000 lines.

## Dependency Impact

- Subbundle `04` depends on this phase for the final calendar proof and the global no-file-over-2000 closure audit.
- If this phase is weak, the final closure would still leave CanvasLib above the user’s size ceiling.

## Validation Depth

- `UI, test, and browser-proof`

## Implementation Steps

1. Choose calendar split boundaries that isolate runtime entry, rendering, interaction, and shared helpers.
2. Move the calendar source into deeper folders without changing the public loading contract more than necessary.
3. Update the manifest and regenerate public outputs and include components.
4. Run calendar-focused validation and record the result.

## Scope Exceptions

- `none`

## Do Not Do

- Do not treat the calendar file as optional because the workbench split already reduced risk.
- Do not leave any generated calendar file above 2000 lines.
- Do not reorder shared dependencies in a way that breaks workbench or preview assets.

## Acceptance Checklist

- The calendar source tree is split into logical folders and smaller files.
- The generated calendar outputs are also split below 2000 lines.
- The manifest/include ordering remains valid after regeneration.
- The calendar route loads and renders correctly after the split.

## Proof Required

- `npm run canvaslib:build-assets`
- `npm run canvaslib:verify-assets`
- A line-count audit covering the split calendar source and generated output files
- Targeted tests that touch the project calendar route or shared CanvasLib loading
- Browser proof on the calendar route with screenshots and console check

## Browser Validation Logging

- Route: `/projects/{projectId}/calendar`
- Viewports: `1600x900`, `1280x800`
- Required actions: navigate, wait for the calendar surface, inspect console, verify route health after regeneration
- Screenshot paths: `output/playwright/canvaslib-calendar-desktop.png`, `output/playwright/canvaslib-calendar-narrow.png`
- Review questions:
  - Does the calendar route load without missing-script or ordering failures?
  - Is the split calendar asset graph stable at both desktop and narrower widths?

## Progression Gate

- Final closure work may continue only after every calendar JS file in CanvasLib is at or below 2000 lines and the calendar route passes browser proof.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Split the CanvasLib calendar monolith into logical source and public output files, keep the manifest authoritative, and prove the project calendar route still works before starting final closure.
```
