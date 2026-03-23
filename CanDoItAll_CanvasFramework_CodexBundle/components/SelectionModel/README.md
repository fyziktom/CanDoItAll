# SelectionModel

SelectionModel is a P0 shared low-level component in the category `Selection and transform components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Selection and transform components |
| Status | partial |
| Priority | P0 |
| Level | low-level |
| Scope | shared |
| JS bridge | partial |
| Implementation wave | Wave 1 |

## Purpose

Own single-select, multi-select, primary selection, range semantics, and selection-state publication.

## Why this component is needed

Selection state is already central but split across JS state, C# DTOs, and page-level logic. It needs a shared authoritative model.

## Main use cases

- Track primary node and selected node IDs in Project Structure and Prompt Factory.
- Drive inspector content, toolbar enablement, and selection frame rendering.
- Prepare for selection-based copy/paste and transform handles.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

SelectionModel already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/SelectionModel.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/selection-model.js`
- `tests/CanDoItAll.Tests.Components/SelectionModelTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340` — Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. Key symbols: CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572` — Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....

## Related components

- HitTestService
- MarqueeSelectionOverlay
- HoverFocusRouter

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `SelectionModel` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
