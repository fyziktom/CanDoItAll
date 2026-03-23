# AnimationTimeline

AnimationTimeline is a P2 shared low-level component in the category `Advanced graphical components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Advanced graphical components |
| Status | missing |
| Priority | P2 |
| Level | low-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 6 |

## Purpose

Provide a minimal shared animation system for smooth transitions, focus pans, hover fades, and connector or badge micro-animations.

## Why this component is needed

The product wants wow effect and fluidity, but there is no shared animation layer. Without one, every new animation will become bespoke.

## Main use cases

- Animate fit-to-view or focus transitions.
- Fade in selection overlays or guide lines.
- Support subtle connector flow animations or badge state transitions.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

AnimationTimeline is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Core/AnimationTimeline.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/animation-timeline.js`
- `tests/CanDoItAll.Tests.Components/AnimationTimelineTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `docs/canvases-improvements/02-shared-canvas-system-spec.md#L1-L430` — Existing shared canvas system specification that already separates Blazor-owned and JS-owned responsibilities. Key symbols: Shared architecture, JavaScript owns, Blazor owns.

## Related components

- InvalidationScheduler
- ViewportController
- CanvasThemeTokenPack

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `AnimationTimeline` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
