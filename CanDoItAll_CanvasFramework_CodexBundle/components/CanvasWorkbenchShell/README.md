# CanvasWorkbenchShell

CanvasWorkbenchShell is a P0 shared high-level component in the category `Layout and navigation components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Layout and navigation components |
| Status | exists |
| Priority | P0 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 2 |

## Purpose

Primary reusable graph-workbench component that hosts the graph runtime, toolbar affordances, zoom rail, and typed events.

## Why this component is needed

This already exists and is the correct strategic starting point, but it needs internal decomposition so it can become the long-lived shell of the graph framework.

## Main use cases

- Project Structure graph editing.
- Prompt Factory canvas editing.
- Future graph-like editors such as dependency maps or visual planners.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

CanvasWorkbenchShell already exists in the repository and should be treated as the extraction point, not replaced by a parallel implementation.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.ComponentKit/Canvas/Graph/CanvasWorkbenchShell.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`
- `tests/CanDoItAll.Tests.Components/CanvasWorkbenchTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572` — Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync, ....
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340` — Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. Key symbols: CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.

## Related components

- CanvasSceneHost
- ViewportController
- SelectionModel
- ContextMenuHost
- CreateActionPalette

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `CanvasWorkbenchShell` boundary in the recommended target path(s).
3. Preserve the current public usage surface where possible. Refactor internals, split responsibilities, and add tests instead of forcing a broad page-level API rewrite.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Preserve the current public usage surface where possible. Refactor internals, split responsibilities, and add tests instead of forcing a broad page-level API rewrite.
