# Integration Strategy

## Core rule

The integration strategy is **extend the current shared base, extract page-owned logic, retire legacy duplicates**.

## Non-negotiable decisions

- Use `CanvasWorkbench` as the graph-workbench base. Do not create another parallel graph shell.
- Use `CanvasCalendar` as the shared calendar wrapper. Do not continue investing in `ProjectEventsCalendar` as a strategic path.
- Move graph projection, placement, and action catalogs into domain adapters instead of keeping them in page files.
- Split the generic workbench JS runtime by concern before layering in more advanced features.

## Integration seams by page

### Project Structure

- Keep page composition and inspector rendering in `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`.
- Move graph projection and create-placement rules into:
  - `ProjectStructureGraphAdapter`
  - `ProjectStructureActionCatalogAdapter`
  - `ProjectStructurePlacementPolicy`
- Keep service calls in `ProjectWorkbenchService`, but pass them through typed adapter outputs.

### Prompt Factory

- Keep overall page composition in `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`.
- Move graph projection and history logic into:
  - `PromptFactorySessionGraphAdapter`
  - `PromptFactoryCatalogToolbox`
  - `PromptFactoryUndoRedoAdapter`
- Keep domain build/export/send behavior in `PromptFactoryService`.

### Project Calendar

- Replace `ProjectEventsCalendar` usage in `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor` with `CanvasCalendar`.
- Introduce `ProjectCalendarAdapter` and `ProjectCalendarStateParser`.
- Remove raw JSON probing from the page.

## JS runtime integration rules

- Split generic workbench runtime modules before adding new advanced interactions.
- Move Prompt Factory-specific helpers out of generic `canvasWorkbenchInterop.js`.
- Keep calendar runtime specialized, but modularize wrapper-facing concerns and state parsing.
- Never add a domain-specific helper to a generic runtime file without an explicit boundary review.
