# ProjectCalendarStateParser

ProjectCalendarStateParser is a P1 domain-specific low-level component in the category `Calendar domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Calendar domain components |
| Status | missing |
| Priority | P1 |
| Level | low-level |
| Scope | domain-specific |
| JS bridge | none |
| Implementation wave | Wave 5 |

## Purpose

Parse and normalize persisted project calendar view state into a typed model, replacing manual JSON probing in the page.

## Why this component is needed

ProjectCalendarPage currently uses TryReadSelectedEventId over raw JSON. That is brittle and should be replaced by a typed parser/policy.

## Main use cases

- Read selected event ID, preferred view, visible date, and scope from persisted JSON.
- Provide defaults when no state exists or the schema is older than current expectations.
- Support future migration of view-state shape without page-level hacks.

## Architectural context

Domain adapter layer inside `CanDoItAll.Modules.Workbench` bridging project calendar state and the shared calendar wrapper.

## Current-state summary

ProjectCalendarStateParser is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.Modules.Workbench/Calendar/ProjectCalendarStateParser.cs`
- `tests/CanDoItAll.Tests.Components/ProjectCalendarStateParserTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161` — Project calendar page that still uses the legacy calendar wrapper and manually parses selected event IDs from JSON view state. Key symbols: LoadAsync, HandleViewStateChangedAsync, TryReadSelectedEventId.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335` — Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. Key symbols: safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState, ....
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223` — Defines the typed wrapper contract for the shared calendar runtime: surface, event model, playlist models, operation context, selection/state callbacks, and export request. Key symbols: CanvasCalendarSurface, CanvasCalendarEvent, CanvasCalendarPlaylist, CanvasCalendarConnectedEvent, CanvasCalendarChecklistRow, CanvasCalendarOperationContext, ....
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806` — Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. Key symbols: ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest, ....

## Related components

- SerializationPersistencePack
- ProjectCalendarAdapter

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `ProjectCalendarStateParser` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Update `ProjectCalendarPage.razor` so legacy wrapper or raw-state logic is replaced by the shared calendar adapter path.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
