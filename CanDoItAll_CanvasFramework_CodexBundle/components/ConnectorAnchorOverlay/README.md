# ConnectorAnchorOverlay

ConnectorAnchorOverlay is a P1 shared high-level component in the category `Connector and relationship components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Connector and relationship components |
| Status | missing |
| Priority | P1 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 6 |

## Purpose

Show connector anchor points, connection affordances, and routing handles on selected or hovered nodes.

## Why this component is needed

Future connector authoring and routing improvements need explicit anchor visuals and hit regions, which do not exist yet.

## Main use cases

- Create or reroute dependencies in Project Structure.
- Connect prompt flow nodes, branches, or future validation overlays in Prompt Factory.
- Debug connector origin/target geometry during QA.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

ConnectorAnchorOverlay is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/ConnectorAnchorOverlay.cs`
- `src/CanDoItAll.ComponentKit/Components/ConnectorAnchorOverlay.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/connector-anchor-overlay.js`
- `tests/CanDoItAll.Tests.Components/ConnectorAnchorOverlayTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340` — Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. Key symbols: CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction, ....

## Related components

- ConnectorPathPrimitive
- HitTestService
- HoverFocusRouter
- DiagnosticsOverlay

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `ConnectorAnchorOverlay` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
