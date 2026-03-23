# HoverFocusRouter

HoverFocusRouter is a P1 shared low-level component in the category `Interactive components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Interactive components |
| Status | partial |
| Priority | P1 |
| Level | low-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 1 |

## Purpose

Coordinate hover, focus, and active-target visuals across nodes, connectors, menus, and editor overlays.

## Why this component is needed

Hover and focus are currently handled locally by DOM events and CSS. A shared router is needed to keep complex overlays and keyboard interactions coherent.

## Main use cases

- Highlight a hovered node while suppressing stale hover when a context menu opens.
- Transfer focus between canvas node, inline editor, and floating inspector without losing semantic selection.
- Prepare for tooltip/popover timing and handle hover-intent logic.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

HoverFocusRouter already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/HoverFocusRouter.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/hover-focus-router.js`
- `tests/CanDoItAll.Tests.Components/HoverFocusRouterTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37` — Floating inspector docking logic used by the prompt factory canvas. Key symbols: DockCanvasInspectorAsync, SyncFloatingInspectorAsync.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234` — Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. Key symbols: UndoAsync, RedoAsync, OnAfterRenderAsync.

## Related components

- SelectionModel
- TooltipPopoverHost
- AccessibilityMirrorLayer

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `HoverFocusRouter` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
