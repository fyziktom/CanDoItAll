# TextMeasureService File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099 | Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. | mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames... |
| src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720 | Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. | CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render... |
| src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309 | Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. | cw-* CSS rules |
| docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203 | Existing internal analysis of reference workbench behavior and gaps. | Reference capability inventory, Page shell and layout, Canvas host and chrome |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.
