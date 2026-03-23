# Wave-Level Validation Prompts

## Validation prompt template

Review the implementation against these questions:

- Did the change reuse and harden the existing shared framework rather than creating a duplicate path?
- Did the change remove the responsibility from the old location, or does page/local leakage still remain?
- Are business rules still in C# and hot-path rendering/event math in JS?
- Are typed contracts/versioned state models used instead of brittle ad hoc JSON parsing?
- Were tests added or updated at the right boundary?
- Does the UX remain consistent with the shared workbench/calendar family?
- Are performance implications explicit and acceptable?

## Wave-specific validation additions

### Wave 1

- Confirm prompt-factory-specific helper exports no longer live inside the generic graph runtime module.
- Confirm host lifecycle is unified across graph and calendar wrappers.

### Wave 2

- Confirm node card, context menu, create palette, and inline editor responsibilities are no longer hidden in monolithic runtime code blocks.
- Confirm public page usage is stable or intentionally migrated with clear rationale.

### Wave 3

- Confirm `ProjectStructurePage.razor` no longer owns graph projection and placement logic.
- Confirm service wiring still persists movement and view state correctly.

### Wave 4

- Confirm `PromptFactoryPage.razor` no longer owns graph projection and page-local history islands.
- Confirm undo/redo restores both domain state and canvas-relevant state.

### Wave 5

- Confirm `ProjectCalendarPage.razor` uses `CanvasCalendar` and no longer manually parses selected event IDs from raw JSON.
- Confirm legacy wrapper usage is removed or clearly deprecated.

### Wave 6

- Confirm advanced interactions are built on shared primitives and not on new page-local hacks.
- Confirm diagnostics and performance hooks exist for hot-path features.
