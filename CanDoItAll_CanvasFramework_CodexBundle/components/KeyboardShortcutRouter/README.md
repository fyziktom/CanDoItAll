# KeyboardShortcutRouter

KeyboardShortcutRouter is a P1 shared high-level component in the category `Interactive components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Interactive components |
| Status | partial |
| Priority | P1 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 1 |

## Purpose

Standardize canvas keyboard shortcuts, scope management, and conflict handling across graph and calendar surfaces.

## Why this component is needed

Prompt Factory already registers custom shortcuts while the workbench runtime also handles zoom/help keys. This should be formalized.

## Main use cases

- Undo/redo, fit view, zoom in/out, focus primary node, and open help overlay.
- Context-sensitive shortcuts that change between graph editing and inline editing modes.
- Future clipboard, selection transform, and command palette shortcuts.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

KeyboardShortcutRouter already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/KeyboardShortcutRouter.cs`
- `src/CanDoItAll.ComponentKit/Components/KeyboardShortcutRouter.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/keyboard-shortcut-router.js`
- `tests/CanDoItAll.Tests.Components/KeyboardShortcutRouterTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234` — Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. Key symbols: UndoAsync, RedoAsync, OnAfterRenderAsync.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572` — Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....

## Related components

- SelectionModel
- HoverFocusRouter
- CommandHistoryStore

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `KeyboardShortcutRouter` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
