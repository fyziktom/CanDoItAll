# SnapGuideSystem

SnapGuideSystem is a P1 shared low-level component in the category `Selection and transform components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Selection and transform components |
| Status | missing |
| Priority | P1 |
| Level | low-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 6 |

## Purpose

Calculate snapping candidates, alignment guides, and near-edge/center snapping behavior during move and resize operations.

## Why this component is needed

Snapping and alignment guides are explicitly needed for future features but absent from the current implementation.

## Main use cases

- Snap project nodes to grid or sibling edges.
- Align prompt nodes and branch lanes visually during drag.
- Show guide lines and distance hints for precise editing.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

SnapGuideSystem is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/SnapGuideSystem.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/snap-guide-system.js`
- `tests/CanDoItAll.Tests.Components/SnapGuideSystemTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203` — Existing internal analysis of reference workbench behavior and gaps. Key symbols: Reference capability inventory, Page shell and layout, Canvas host and chrome.

## Related components

- GridBackdrop
- SelectionModel
- DragDropController
- ViewportController

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `SnapGuideSystem` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
