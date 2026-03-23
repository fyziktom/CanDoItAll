# CanvasCalendarHost

CanvasCalendarHost is a P0 shared high-level component in the category `Calendar domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Calendar domain components |
| Status | partial |
| Priority | P0 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 5 |

## Purpose

Primary reusable Blazor component that hosts the calendar runtime with typed event/save/delete/export callbacks.

## Why this component is needed

This wrapper already exists and should remain the migration target, but it needs clearer decomposition and shared host conventions.

## Main use cases

- Project calendar integration.
- Potential future calendar views in other modules.
- Export-ready typed wrapper around the specialized JS calendar engine.

## Architectural context

Shared calendar subsystem in `CanDoItAll.ComponentKit`, mounted through the typed `CanvasCalendar` wrapper family.

## Current-state summary

CanvasCalendarHost already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`
- `src/CanDoItAll.ComponentKit/Canvas/Calendar/CanvasCalendarHost.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js`
- `tests/CanDoItAll.Tests.Components/CanvasCalendarTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258` — Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync.
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223` — Defines the typed wrapper contract for the shared calendar runtime: surface, event model, playlist models, operation context, selection/state callbacks, and export request. Key symbols: CanvasCalendarSurface, CanvasCalendarEvent, CanvasCalendarPlaylist, CanvasCalendarConnectedEvent, CanvasCalendarChecklistRow, CanvasCalendarOperationContext, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335` — Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. Key symbols: safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md#L1-L186` — Existing design note arguing for a full-widget Blazor wrapper around the calendar runtime before deeper rewrite. Key symbols: Core recommendation, Target architecture.

## Related components

- CanvasSceneHost
- CalendarCrudBridge
- CalendarSelectionPanel

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `CanvasCalendarHost` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Update `ProjectCalendarPage.razor` so legacy wrapper or raw-state logic is replaced by the shared calendar adapter path.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
