# CanvasSceneHost File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572 | Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. | OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync... |
| src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258 | Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. | OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335 | Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. | safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState... |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099 | Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. | mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames... |
| docs/canvases-improvements/02-shared-canvas-system-spec.md#L1-L430 | Existing shared canvas system specification that already separates Blazor-owned and JS-owned responsibilities. | Shared architecture, JavaScript owns, Blazor owns |
| docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md#L1-L186 | Existing design note arguing for a full-widget Blazor wrapper around the calendar runtime before deeper rewrite. | Core recommendation, Target architecture |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.
