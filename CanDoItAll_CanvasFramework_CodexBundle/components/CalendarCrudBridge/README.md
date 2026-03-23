# CalendarCrudBridge

CalendarCrudBridge is a P0 shared low-level component in the category `Calendar domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Calendar domain components |
| Status | partial |
| Priority | P0 |
| Level | low-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 5 |

## Purpose

Bridge save, delete, playlist search/mutation, and export operations between the JS calendar widget and C# services.

## Why this component is needed

CRUD interop exists but should be explicitly owned as a bridge component to avoid spreading calendar operation details across the wrapper.

## Main use cases

- Forward typed save/delete requests to ProjectWorkbenchService.
- Forward playlist mutation and export requests from the widget.
- Serve as the stable seam for future validation and telemetry.

## Architectural context

Shared calendar subsystem in `CanDoItAll.ComponentKit`, mounted through the typed `CanvasCalendar` wrapper family.

## Current-state summary

CalendarCrudBridge already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarCrudBridge.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-crud-bridge.js`
- `tests/CanDoItAll.Tests.Components/CalendarCrudBridgeTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258` — Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335` — Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. Key symbols: safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState, ....
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223` — Defines the typed wrapper contract for the shared calendar runtime: surface, event model, playlist models, operation context, selection/state callbacks, and export request. Key symbols: CanvasCalendarSurface, CanvasCalendarEvent, CanvasCalendarPlaylist, CanvasCalendarConnectedEvent, CanvasCalendarChecklistRow, CanvasCalendarOperationContext, ....
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806` — Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. Key symbols: ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest, ....

## Related components

- JsInteropBridge
- SerializationPersistencePack

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `CalendarCrudBridge` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Update `ProjectCalendarPage.razor` so legacy wrapper or raw-state logic is replaced by the shared calendar adapter path.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
