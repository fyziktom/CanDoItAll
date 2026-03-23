# PromptRunBranchLane

PromptRunBranchLane is a P1 domain-specific high-level component in the category `Prompt Factory domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Prompt Factory domain components |
| Status | partial |
| Priority | P1 |
| Level | high-level |
| Scope | domain-specific |
| JS bridge | partial |
| Implementation wave | Wave 4 |

## Purpose

Represent and render branch lanes or grouped prompt-run paths within the shared graph workbench.

## Why this component is needed

Prompt Factory already uses branch-specific layout ideas, but they are not formalized as a reusable lane component.

## Main use cases

- Visualize alternate prompt branches or outcomes.
- Keep branch-specific nodes aligned and grouped.
- Support future reorder or branch template flows.

## Architectural context

Domain adapter layer inside `CanDoItAll.Modules.Factory` bridging prompt-session state and the shared graph framework.

## Current-state summary

PromptRunBranchLane already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptRunBranchLane.cs`
- `src/CanDoItAll.Modules.Factory/wwwroot/js/prompt-run-branch-lane.js`
- `tests/CanDoItAll.Tests.Components/PromptRunBranchLaneTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090` — Selection graph builder and catalog behavior for the prompt factory canvas. Key symbols: BuildSelectionGraph, HandleCatalogContextActionAsync, HandleCatalogCreateActionAsync.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....

## Related components

- LayoutEngine
- GroupFrameOverlay
- TextBlockPrimitive

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `PromptRunBranchLane` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Update `PromptFactoryPage` partial files so graph projection/history/toolbox behavior is consumed through the adapter boundary.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
