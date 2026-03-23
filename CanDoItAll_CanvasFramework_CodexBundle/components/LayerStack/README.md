# LayerStack

LayerStack is a P0 shared low-level component in the category `Utility and infrastructure components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Utility and infrastructure components |
| Status | missing |
| Priority | P0 |
| Level | low-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 1 |

## Purpose

Model explicit rendering layers for background, connectors, node cards, overlays, selection tools, diagnostics, and accessibility mirrors.

## Why this component is needed

The current workbench JS has implicit layers inside one DOM/SVG runtime. Explicit layering is required for predictable z-order, event routing, and performance tuning.

## Main use cases

- Keep grid and guides below interactive content while keeping selection tools and context layers above it.
- Swap connector rendering between SVG and canvas without changing page contracts.
- Enable partial redraw rules by layer instead of full-scene repaint.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

LayerStack is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Core/LayerStack.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/layer-stack.js`
- `tests/CanDoItAll.Tests.Components/LayerStackTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `docs/canvases-improvements/02-shared-canvas-system-spec.md#L1-L430` — Existing shared canvas system specification that already separates Blazor-owned and JS-owned responsibilities. Key symbols: Shared architecture, JavaScript owns, Blazor owns.

## Related components

- CanvasSceneHost
- SceneNodeModel
- InvalidationScheduler

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `LayerStack` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
