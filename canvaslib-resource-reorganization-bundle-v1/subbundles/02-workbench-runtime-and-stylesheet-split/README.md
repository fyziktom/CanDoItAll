# Workbench runtime and stylesheet split

## Status

- `Completed`

## Objective

- Split the CanvasLib workbench runtime and workbench stylesheet monoliths into logical folders and smaller source files, then split the generated public runtime and stylesheet outputs so both source and generated CanvasLib files remain below 2000 lines.

## Covered Inputs

- `N002 Split canvasWorkbenchInterop.js into logical parts`
- `N003 Split CanvasLib wwwroot JS and CSS resources into folders`
- `R02`
- `R03`
- `R04`
- `R05`
- `R08`
- `R09`

## Prerequisites

- `subbundles/01-asset-topology-and-duplicate-retirement` completed with a passed closure gate

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\workbench`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css-src\workbench`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench`
- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json`
- `C:\repositories\CanDoItAll\tools\canvaslib\build-assets.cjs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibHeadAssets.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibBodyAssets.razor`

## Deliverables

- A deeper workbench source tree under `wwwroot\js-src\runtime\workbench\**`.
- A deeper workbench stylesheet source tree under `wwwroot\css-src\workbench\**`.
- Updated manifest entries and regenerated include components for the split workbench assets.
- Generated public workbench JS and CSS outputs that stay below 2000 lines per file.

## Dependency Impact

- Subbundle `03` depends on the manifest/include-generation machinery changed here.
- Subbundle `04` depends on this phase for the main structure-canvas runtime proof and the final line-count closure audit.
- If this phase is weak, later calendar proof may pass while the workbench route is still broken or loading assets out of order.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Choose workbench split boundaries that map to clear responsibilities such as shared helpers, state, render, interaction, overlays, export, and runtime entry.
2. Move the workbench JS source into those folders and preserve load order through the manifest.
3. Split the workbench stylesheet into smaller files under logical CSS folders and map them to smaller public outputs.
4. Regenerate CanvasLib assets and include components.
5. Run targeted validation on the structure canvas route before allowing calendar changes.

## Scope Exceptions

- Preview assets may stay in their current structure if they are not needed for the workbench split.

## Do Not Do

- Do not leave a generated public `canvasWorkbenchInterop.js` or `canvas-workbench.css` file above 2000 lines.
- Do not introduce a second asset-loading mechanism beside the manifest and generated include components.
- Do not refactor workbench feature behavior beyond what is necessary to preserve the current runtime after the split.

## Acceptance Checklist

- Workbench runtime source files live in deeper responsibility-based folders.
- Workbench stylesheet source files live in deeper responsibility-based folders.
- The manifest and generated include components list the split files in a stable, dependency-safe order.
- No generated workbench JS or CSS file exceeds 2000 lines.
- The structure canvas route loads and behaves correctly after regeneration.

## Proof Required

- `npm run canvaslib:build-assets`
- `npm run canvaslib:verify-assets`
- A line-count audit covering the split workbench source and generated output files
- Targeted tests that exercise CanvasLib consumers
- Browser proof on the structure route with screenshots and console check

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`
- Viewports: `1600x900`, `1280x800`
- Required actions: wait for `.cw-workbench-shell`, open quick create, open help, inspect console, verify no circuit failure
- Screenshot paths: `output/playwright/canvaslib-workbench-structure-desktop.png`, `output/playwright/canvaslib-workbench-structure-narrow.png`
- Review questions:
  - Does the structure canvas still load without missing-script errors?
  - Does the split asset order preserve menu and help interactions?
  - Does the shell remain visually intact at desktop and narrower widths?

## Progression Gate

- Calendar work may continue only after the workbench assets regenerate cleanly, the structure route passes browser proof, and every workbench JS/CSS file in CanvasLib is at or below 2000 lines.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Split the CanvasLib workbench runtime and stylesheet monoliths into logical source and public output files, keep manifest ordering authoritative, and prove the structure route still works before moving to calendar assets.
```
