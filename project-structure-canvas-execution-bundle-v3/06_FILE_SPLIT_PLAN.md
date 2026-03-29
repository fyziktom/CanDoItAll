# File split plan

## Why split now

The next renderer phase will add complexity.  
If the code stays in the current monoliths, every performance fix becomes harder to review and easier to break.

## Current monoliths

| File | Current lines | Problem |
| --- | --- | --- |
| src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor | 723 | Monolithic markup/code-behind and still hosts a div-based scene rather than a real canvas stage. |
| src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js | 6648 | Giant DOM/SVG-centric monolith with mixed responsibilities and partial retained rendering only. |
| src/CanDoItAll.Components.CanvasLib/wwwroot/canvas-workbench.css | 4324 | Huge mixed-ownership stylesheet. |
| src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor | 297 | Needs stronger overlay-ownership contract and its own local tests around scroll/wheel/drag interplay. |
| src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvas-floating-window.js | 545 | Still large enough to benefit from helper extraction and source-fragment generation. |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor | 1808 | Still too large; toolbox and several windows/dialogs should be dedicated child components. |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css | 823 | Large page-scoped CSS file that should shrink once windows move into their own components. |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ToolWindows.cs | 138 | Current accordion semantics are insufficient and mixed with the page rather than a dedicated component model. |

## Source-file budget rules

| File type | Preferred budget | Hard cap | Notes |
| --- | --- | --- | --- |
| Plain JS source fragment | target <= 350 lines | hard cap 550 lines | Generated public bundle may exceed this. |
| CSS source fragment | target <= 250 lines | hard cap 400 lines | Prefer one concern per file. |
| Razor component markup | target <= 250 lines | hard cap 400 lines | Split window/dialog regions into child components. |
| Partial C# file | target <= 250 lines | hard cap 400 lines | Large page behaviors should be grouped by feature. |
| Generated public JS/CSS bundle | no strict source budget | must be generated, not hand-edited | Review source fragments instead of reviewing generated monoliths. |

## JavaScript split blueprint

| Target source file/group | Responsibility | Current origin |
| --- | --- | --- |
| shared/utils.js | DOM helpers, clamp/round/debounce, common math, image loading, canvas scaling helpers | Many current functions in lines 1-250 and export helpers |
| workbench/core/metrics.js | metrics store, counters, durations, snapshots | createWorkbenchMetrics and related helpers |
| workbench/state/normalize.js | surface normalization, action normalization, lookup building | input and surface normalization functions |
| workbench/runtime/projection.js | scene bounds, viewport projection, visible node/link filtering | lines ~557-1140 |
| workbench/render/links.js | canvas link rendering and link hit metadata | replaces current SVG link layer functions |
| workbench/render/frames.js | canvas group frame rendering | replaces current frame div layer |
| workbench/render/nodes.js | canvas node card rendering and LOD | replaces DOM node card build path |
| workbench/render/overlays.js | marquee, snap guides, hover rings, diagnostics marks | canvas overlay layer |
| workbench/interaction/hit-testing.js | geometry-based hot-zones and hit routines | replaces DOM-target hit testing |
| workbench/interaction/viewport.js | pan, zoom, fit, focus, minimap navigation | viewport controller |
| workbench/interaction/drag-and-marquee.js | drag loop, marquee, dirty-region invalidation | pointermove hot path |
| workbench/overlays/context-menu.js | HTML context menu placement and layers | keep HTML, but separate from renderer |
| workbench/overlays/composer.js | create composer, inline note editor, active HTML overlays | HTML overlay logic |
| workbench/export/export-scene.js | direct canvas export composition | replaces DOM clone export |
| workbench/runtime/entry.js | public create/update/dispose/bootstrap and renderer selection | stable external API |

### Important rule
The generated public bundle can remain large, but **source** fragments must stay small and single-purpose.

## CSS split blueprint

| Target source file | Responsibility |
| --- | --- |
| workbench-shell.css | shell, frame, stage root |
| workbench-toolbar.css | toolbar and zoom controls |
| workbench-stage.css | canvas stack, overlay root, accessibility root |
| workbench-floating-window.css | floating window shared styles |
| workbench-toolbox.css | toolbox window layout and VS-like list styling |
| workbench-diagnostics.css | diagnostics shell and counters |
| workbench-popovers.css | tooltips, popovers, context menu |
| workbench-responsive.css | responsive overrides |

## Razor / C# split blueprint

| Target file | Responsibility |
| --- | --- |
| CanvasWorkbench.razor | Small public shell only |
| CanvasWorkbench.razor.cs | lifecycle, JS interop, event plumbing |
| CanvasWorkbenchToolbar.razor | toolbar markup |
| CanvasWorkbenchHelpOverlay.razor | help overlay |
| CanvasWorkbenchSettingsOverlay.razor | settings overlay |
| ProjectStructurePage.razor | small page host only |
| ProjectStructureToolboxWindow.razor | toolbox window markup |
| ProjectStructureHealthWindow.razor | health window markup |
| ProjectStructureSelectionWindow.razor | selection window markup |
| ProjectStructureDialogs.razor | summary/transcript/mermaid/preview dialogs |

## Additional ProjectStructurePage decomposition guidance

### Move out of the main page host
- toolbox window,
- health window,
- selection window,
- dialog cluster,
- support cards below the canvas.

### Keep in the page host
- route parameters,
- high-level orchestration,
- page-level services,
- shared selection/current node references,
- renderer adoption wiring.

## Review rule
Generated public outputs should be reviewed only for sanity.  
Most code review attention should focus on the smaller source fragments.
