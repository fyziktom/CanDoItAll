# ProjectCalendarAdapter

ProjectCalendarAdapter is a P0 domain-specific high-level component in the category `Calendar domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Calendar domain components |
| Status | missing |
| Priority | P0 |
| Level | high-level |
| Scope | domain-specific |
| JS bridge | none |
| Implementation wave | Wave 5 |

## Purpose

Map project-specific calendar domain models and view-state persistence to the shared CanvasCalendar contract.

## Why this component is needed

ProjectCalendarPage still uses a legacy wrapper and string parsing. A dedicated adapter is required to finish the migration cleanly.

## Main use cases

- Map ProjectCalendarSurface and ProjectCalendarEvent to CanvasCalendarSurface and CanvasCalendarEvent.
- Persist view state through ProjectWorkbenchService using typed calendar state objects.
- Hide legacy wrapper details from the page.

## Architectural context

Domain adapter layer inside `CanDoItAll.Modules.Workbench` bridging project calendar state and the shared calendar wrapper.

## Current-state summary

ProjectCalendarAdapter is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.Modules.Workbench/Calendar/ProjectCalendarAdapter.cs`
- `tests/CanDoItAll.Tests.Components/ProjectCalendarAdapterTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161` — Project calendar page that still uses the legacy calendar wrapper and manually parses selected event IDs from JSON view state. Key symbols: LoadAsync, HandleViewStateChangedAsync, TryReadSelectedEventId.
- `src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor#L1-L79` — Legacy project calendar wrapper using the old workbench JS runtime. This is the primary migration target for adopting CanvasCalendar. Key symbols: OnAfterRenderAsync, CanDoItAll.workbenchCalendar.create, CanDoItAll.workbenchCalendar.update, CanDoItAll.workbenchCalendar.dispose.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806` — Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. Key symbols: ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258` — Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync.
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223` — Defines the typed wrapper contract for the shared calendar runtime: surface, event model, playlist models, operation context, selection/state callbacks, and export request. Key symbols: CanvasCalendarSurface, CanvasCalendarEvent, CanvasCalendarPlaylist, CanvasCalendarConnectedEvent, CanvasCalendarChecklistRow, CanvasCalendarOperationContext, ....

## Related components

- CanvasCalendarHost
- SerializationPersistencePack
- CalendarCrudBridge

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `ProjectCalendarAdapter` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Update `ProjectCalendarPage.razor` so legacy wrapper or raw-state logic is replaced by the shared calendar adapter path.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
