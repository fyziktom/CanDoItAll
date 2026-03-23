# TransformHandlesOverlay File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099 | Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. | mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames... |
| src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js#L1-L884 | Legacy graph and calendar runtime. Contains older canvas drawing helpers, hit testing, and duplicate calendar wrapper logic that should not continue as the strategic base. | safeParse, getCanvasNodeBounds, drawRoundedRect, drawDiamond, drawHex, drawShield... |
| docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203 | Existing internal analysis of reference workbench behavior and gaps. | Reference capability inventory, Page shell and layout, Canvas host and chrome |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.
