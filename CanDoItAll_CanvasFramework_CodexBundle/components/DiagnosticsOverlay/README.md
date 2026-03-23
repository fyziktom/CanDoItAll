# DiagnosticsOverlay

DiagnosticsOverlay is a P1 shared high-level component in the category `Diagnostic and developer components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Diagnostic and developer components |
| Status | missing |
| Priority | P1 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 6 |

## Purpose

Surface debug information for scene bounds, layer order, hit regions, selection, frame timing, and invalidation reasons.

## Why this component is needed

The current system has no formal debug or profiling hooks, making performance and correctness issues harder to inspect.

## Main use cases

- Toggle overlays that show node bounds, connector anchors, and selection rectangles.
- Display frame timing and dirty-layer counters during drag and zoom tuning.
- Enable QA and future Codex agents to validate event routing and visual bounds without reverse-engineering DOM internals.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

DiagnosticsOverlay is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Diagnostics/DiagnosticsOverlay.cs`
- `src/CanDoItAll.ComponentKit/Components/DiagnosticsOverlay.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/diagnostics-overlay.js`
- `tests/CanDoItAll.Tests.Components/DiagnosticsOverlayTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `docs/canvases-improvements/04-implementation-plan.md#L1-L334` — Existing implementation plan for the shared canvas direction. Helpful for sequencing and validating the migration roadmap. Key symbols: Phase 1: Build the shared canvas foundation.

## Related components

- CanvasSceneHost
- LayerStack
- InvalidationScheduler

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `DiagnosticsOverlay` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
