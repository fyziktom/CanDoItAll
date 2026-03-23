# MarqueeSelectionOverlay

MarqueeSelectionOverlay is a P1 shared high-level component in the category `Selection and transform components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Selection and transform components |
| Status | partial |
| Priority | P1 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 6 |

## Purpose

Render and manage the marquee selection rectangle and intersection query for multi-selection.

## Why this component is needed

Marquee selection already exists in the shared runtime but is not isolated as a reusable overlay component with explicit policy knobs.

## Main use cases

- Alt-drag selection in the shared workbench.
- Potential future touch-lasso or box-select gestures.
- Debugging of intersection behavior and future selection filters.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

MarqueeSelectionOverlay already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/MarqueeSelectionOverlay.cs`
- `src/CanDoItAll.ComponentKit/Components/MarqueeSelectionOverlay.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/marquee-selection-overlay.js`
- `tests/CanDoItAll.Tests.Components/MarqueeSelectionOverlayTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572` — Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync, ....

## Related components

- SelectionModel
- HitTestService
- LayerStack

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `MarqueeSelectionOverlay` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
