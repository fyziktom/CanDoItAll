# TransformHandlesOverlay

TransformHandlesOverlay is a P1 shared high-level component in the category `Selection and transform components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Selection and transform components |
| Status | missing |
| Priority | P1 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 6 |

## Purpose

Render resize, rotate, and move handles around selected objects or groups.

## Why this component is needed

Transform handles are not present today but are an expected next-step capability for richer editors and image/media nodes.

## Main use cases

- Resize or rotate future image and grouped nodes.
- Scale selection frames in advanced project and prompt editors.
- Provide a shared transform interaction language that mirrors mature canvas frameworks.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

TransformHandlesOverlay is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/TransformHandlesOverlay.cs`
- `src/CanDoItAll.ComponentKit/Components/TransformHandlesOverlay.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/transform-handles-overlay.js`
- `tests/CanDoItAll.Tests.Components/TransformHandlesOverlayTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js#L1-L884` — Legacy graph and calendar runtime. Contains older canvas drawing helpers, hit testing, and duplicate calendar wrapper logic that should not continue as the strategic base. Key symbols: safeParse, getCanvasNodeBounds, drawRoundedRect, drawDiamond, drawHex, drawShield, ....
- `docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203` — Existing internal analysis of reference workbench behavior and gaps. Key symbols: Reference capability inventory, Page shell and layout, Canvas host and chrome.

## Related components

- SelectionModel
- HitTestService
- DragDropController
- ConnectorAnchorOverlay

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `TransformHandlesOverlay` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
