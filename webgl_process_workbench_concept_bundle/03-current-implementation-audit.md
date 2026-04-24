# Current implementation audit

## Repo-backed anchors

The concept branch should treat the following repository seams as its main reference points:

- `src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor` already demonstrates a typed Blazor wrapper over a JS runtime.
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js` already exposes semantic automation helpers such as scene snapshot and drag simulation.
- `src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs` and `ProcessCanvasBranching.cs` already define stable node IDs, connection categories, and branch semantics.
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs` and `ProcessDependencyCompatibilityBridge.cs` already allow real template-backed editor models.
- `Templates/Processes/manifest.json` already contains multiple representative process templates with different complexity levels.
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs` already proves that the repository prefers semantic JS helpers over raw pixel-only canvas control.

## High-value current strengths

- Typed surface contracts already exist in the repository and can be mirrored for WebGL.
- Template data is real and already aligned with the current Processes architecture.
- Playwright tests already read semantic state from the current canvas runtime.
- The existing canvas pattern already separates C# orchestration from JS rendering work.

## Current constraints

- The current canvas/runtime and process-workspace areas are already large hotspots, so the concept should avoid broad production rewrites.
- The current Process workspace is tied to 2D semantics and persistence behavior that are not appropriate as the first WebGL experiment target.
- Browser automation against raw WebGL is weak unless the runtime deliberately exposes proof hooks.
- A naive free-form 3D scene would likely worsen readability for labels and edge routing.

## Observed hotspot file sizes

| Path | Approx. lines |
| --- | --- |
| tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs | 5106 |
| tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs | 1459 |
| src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor | 943 |
| src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07a-runtime-interaction-router.js | 899 |
| src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js | 868 |
| src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07b-runtime-rendering.js | 578 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs | 520 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Links.cs | 512 |
| tests/CanDoItAll.Tests.Components/ProcessCanvasSurfaceFactoryTests.cs | 495 |
| src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs | 488 |
| src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs | 486 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor | 447 |

## Immediate conclusion

The safest concept path is:

1. reuse the current typed-canvas contract shape,
2. build a new universal WebGL library,
3. keep process-specific projection out of that library,
4. validate the concept in a dedicated sandbox with real templates.
