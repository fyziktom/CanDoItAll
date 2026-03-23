# AccessibilityMirrorLayer

AccessibilityMirrorLayer is a P2 shared high-level component in the category `Utility and infrastructure components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Utility and infrastructure components |
| Status | missing |
| Priority | P2 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 6 |

## Purpose

Maintain a hidden but semantic DOM representation of interactive canvas content for screen readers and keyboard navigation fallbacks.

## Why this component is needed

Canvas-heavy editors need deliberate accessibility fallback strategies; none are formalized today.

## Main use cases

- Expose selected node summaries and actionable items to assistive tech.
- Mirror calendar event selection and navigation outside the visual canvas.
- Provide keyboard-only navigation through scene entities when direct canvas semantics are insufficient.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

AccessibilityMirrorLayer is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Core/AccessibilityMirrorLayer.cs`
- `src/CanDoItAll.ComponentKit/Components/AccessibilityMirrorLayer.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/accessibility-mirror-layer.js`
- `tests/CanDoItAll.Tests.Components/AccessibilityMirrorLayerTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572` — Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258` — Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335` — Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. Key symbols: safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState, ....

## Related components

- CanvasSceneHost
- SelectionModel
- HoverFocusRouter
- SerializationPersistencePack

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `AccessibilityMirrorLayer` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
