# CalendarSelectionPanel

CalendarSelectionPanel is a P1 shared high-level component in the category `Calendar domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Calendar domain components |
| Status | partial |
| Priority | P1 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 5 |

## Purpose

Present selected event details, contextual playlists/checklists, and selection-derived actions in the calendar shell.

## Why this component is needed

The calendar engine already renders supporting panels, but their responsibilities are opaque and should become explicit wrapper-level concepts.

## Main use cases

- Show selected event details and connected data in the project calendar.
- Surface playlist or checklist actions through typed callbacks.
- Support future plug-in side panels for event metadata.

## Architectural context

Shared calendar subsystem in `CanDoItAll.ComponentKit`, mounted through the typed `CanvasCalendar` wrapper family.

## Current-state summary

CalendarSelectionPanel already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarSelectionPanel.cs`
- `src/CanDoItAll.ComponentKit/Components/CalendarSelectionPanel.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-selection-panel.js`
- `tests/CanDoItAll.Tests.Components/CalendarSelectionPanelTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223` — Defines the typed wrapper contract for the shared calendar runtime: surface, event model, playlist models, operation context, selection/state callbacks, and export request. Key symbols: CanvasCalendarSurface, CanvasCalendarEvent, CanvasCalendarPlaylist, CanvasCalendarConnectedEvent, CanvasCalendarChecklistRow, CanvasCalendarOperationContext, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258` — Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161` — Project calendar page that still uses the legacy calendar wrapper and manually parses selected event IDs from JSON view state. Key symbols: LoadAsync, HandleViewStateChangedAsync, TryReadSelectedEventId.

## Related components

- CanvasCalendarHost
- TextBlockPrimitive
- ChipBadgePrimitive

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `CalendarSelectionPanel` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Update `ProjectCalendarPage.razor` so legacy wrapper or raw-state logic is replaced by the shared calendar adapter path.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
