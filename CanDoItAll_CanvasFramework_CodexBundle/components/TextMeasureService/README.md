# TextMeasureService

TextMeasureService is a P0 shared low-level component in the category `Text components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Text components |
| Status | missing |
| Priority | P0 |
| Level | low-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 1 |

## Purpose

Provide shared text measurement, wrapping, truncation, ellipsis, multi-line sizing, and font-cache behavior for graph and calendar surfaces.

## Why this component is needed

Text is a central element of every current canvas surface, but measurement rules live implicitly in DOM or specialized JS code. A shared service is required for consistent card sizing and truncation behavior.

## Main use cases

- Measure node titles and subtitles before final card layout.
- Apply consistent multi-line wrapping and ellipsis rules to calendar events and graph cards.
- Support icon+text chips and badges without repeated browser measurement code.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

TextMeasureService is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/TextMeasureService.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/text-measure-service.js`
- `tests/CanDoItAll.Tests.Components/TextMeasureServiceTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.
- `docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203` — Existing internal analysis of reference workbench behavior and gaps. Key symbols: Reference capability inventory, Page shell and layout, Canvas host and chrome.

## Related components

- JsInteropBridge
- CanvasThemeTokenPack

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `TextMeasureService` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
