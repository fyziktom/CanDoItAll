# CalendarMiniMonthNavigator

CalendarMiniMonthNavigator is a P2 shared high-level component in the category `Calendar domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Calendar domain components |
| Status | partial |
| Priority | P2 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 5 |

## Purpose

Provide the mini-month or small navigation calendar used to jump between dates and scopes.

## Why this component is needed

This behavior exists inside the monolithic calendar runtime and should be named explicitly for customization and testing.

## Main use cases

- Jump to dates from the project calendar side panel.
- Sync visible date selection with the main calendar view.
- Support future compact calendar dashboards or embedded mini navigators.

## Architectural context

Shared calendar subsystem in `CanDoItAll.ComponentKit`, mounted through the typed `CanvasCalendar` wrapper family.

## Current-state summary

CalendarMiniMonthNavigator already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarMiniMonthNavigator.cs`
- `src/CanDoItAll.ComponentKit/Components/CalendarMiniMonthNavigator.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-mini-month-navigator.js`
- `tests/CanDoItAll.Tests.Components/CalendarMiniMonthNavigatorTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335` — Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. Key symbols: safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState, ....
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161` — Project calendar page that still uses the legacy calendar wrapper and manually parses selected event IDs from JSON view state. Key symbols: LoadAsync, HandleViewStateChangedAsync, TryReadSelectedEventId.

## Related components

- CanvasCalendarHost
- CalendarSelectionPanel
- CanvasThemeTokenPack

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `CalendarMiniMonthNavigator` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Update `ProjectCalendarPage.razor` so legacy wrapper or raw-state logic is replaced by the shared calendar adapter path.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
