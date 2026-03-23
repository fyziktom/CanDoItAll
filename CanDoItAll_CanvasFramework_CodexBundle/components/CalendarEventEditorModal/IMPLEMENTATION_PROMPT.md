# Implementation Prompt — CalendarEventEditorModal

Implement `CalendarEventEditorModal` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Calendar domain components`
- Status today: `partial`
- Priority: `P1`
- Scope: `shared`
- JS bridge: `required`

## Objective

Encapsulate event create/edit modal behavior, validation, and typed save/delete flows in the calendar subsystem.

## Required behavior

- Create or edit project calendar events.
- Validate required fields and contextual playlist/checklist links.
- Support future event templates or duplicate flows.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarEventEditorModal.cs`
- `src/CanDoItAll.ComponentKit/Components/CalendarEventEditorModal.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-event-editor-modal.js`
- `tests/CanDoItAll.Tests.Components/CalendarEventEditorModalTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `CalendarEventEditorModal` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `CalendarEventEditorModal` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
