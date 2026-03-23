# CanvasSceneHost

CanvasSceneHost is a P0 shared low-level component in the category `Utility and infrastructure components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Utility and infrastructure components |
| Status | partial |
| Priority | P0 |
| Level | low-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 1 |

## Purpose

Provide a unified lifecycle host for canvas-family surfaces: mount, update, resize, theme sync, overlay slots, disposal, and diagnostics hooks.

## Why this component is needed

Today the host lifecycle is duplicated across CanvasWorkbench, CanvasCalendar, and legacy wrappers. A dedicated host contract reduces interop drift and gives every canvas runtime the same mounting discipline.

## Main use cases

- Mount the graph workbench inside a Blazor page and forward typed callbacks to C#.
- Mount the calendar runtime inside the same shell conventions without re-implementing create/update/dispose patterns.
- Expose a stable host handle for diagnostics, overlay layers, resize observers, and test hooks.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

CanvasSceneHost already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Core/CanvasSceneHost.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvas-scene-host.js`
- `tests/CanDoItAll.Tests.Components/CanvasSceneHostTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572` — Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258` — Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335` — Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. Key symbols: safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `docs/canvases-improvements/02-shared-canvas-system-spec.md#L1-L430` — Existing shared canvas system specification that already separates Blazor-owned and JS-owned responsibilities. Key symbols: Shared architecture, JavaScript owns, Blazor owns.
- `docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md#L1-L186` — Existing design note arguing for a full-widget Blazor wrapper around the calendar runtime before deeper rewrite. Key symbols: Core recommendation, Target architecture.

## Related components

- JsInteropBridge
- CanvasThemeTokenPack
- DiagnosticsOverlay

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `CanvasSceneHost` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
