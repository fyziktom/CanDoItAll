# IconGlyphPrimitive File References

| Path | Why it matters | Key symbols / areas |
| --- | --- | --- |
| src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309 | Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. | cw-* CSS rules |
| src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099 | Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. | mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames... |
| src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645 | Prompt Factory domain create/action catalog used to populate canvas context actions and create menus. | BuildSessionContextActions, BuildSelectionContextActions, BuildComponentNodeActions, BuildInputNodeActions |
| src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326 | Domain action catalog and label resolver for Project Structure create flows and inspector create groups. | ResolveNodeLabel, BuildMenuCreateActions, BuildInspectorCreateGroups, TryResolveCreateDefinition |

## Navigation advice

Start with the first file that already owns the most behavior for this component, then inspect the wrapper/page/service files that consume that behavior.
