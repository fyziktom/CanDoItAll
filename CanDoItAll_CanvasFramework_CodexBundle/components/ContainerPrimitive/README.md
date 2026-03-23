# ContainerPrimitive

ContainerPrimitive is a P0 shared low-level component in the category `Containers`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Containers |
| Status | partial |
| Priority | P0 |
| Level | low-level |
| Scope | shared |
| JS bridge | partial |
| Implementation wave | Wave 2 |

## Purpose

Provide a reusable container card/panel surface with padding, elevation, selection styling, clipping, and child layout slots.

## Why this component is needed

Node cards, inspector panels, selection frames, and future popovers all need consistent container rules beyond raw HTML divs.

## Main use cases

- Project Structure node cards and group frames.
- Prompt Factory session, component, branch, and attachment nodes.
- Context popovers, inline editors, and future mini-map frame surfaces.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

ContainerPrimitive already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/ContainerPrimitive.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/container-primitive.js`
- `tests/CanDoItAll.Tests.Components/ContainerPrimitiveTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....

## Related components

- CanvasThemeTokenPack
- TextBlockPrimitive
- IconGlyphPrimitive

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `ContainerPrimitive` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
