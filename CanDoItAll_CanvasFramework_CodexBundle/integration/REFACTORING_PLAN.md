# Refactoring Plan

## Priority refactors

1. Split `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js` into concern-specific modules while preserving the current shared workbench API surface.
2. Extract `MapCanvasNode`, `ResolveCreatePlacement`, and selection-border generation out of `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`.
3. Extract `BuildCanvasNodes`, `BuildCanvasLinks`, `BuildSelectionGraph`, and page-local history behavior out of `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage*.cs` files.
4. Migrate `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor` from `ProjectEventsCalendar` to `CanvasCalendar` through `ProjectCalendarAdapter`.
5. Retire `src/CanDoItAll.Modules.Workbench/Components/ProjectStructureCanvas.razor`, `src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor`, and the legacy `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js` path after migration.

## Concrete extraction targets

### From `ProjectStructurePage.razor`

- `MapCanvasNode`
- `ResolveCreatePlacement`
- `CreateSelectionBorderAsync`
- `ClearSelectionBordersAsync`
- action-tree and create-placement wiring inside create handlers

### From `PromptFactoryPage.razor` and friends

- `BuildCanvasNodes`
- `BuildCanvasLinks`
- `BuildSelectionGraph`
- page-local history stack orchestration
- floating-inspector helper coupling

### From shared JS

- Separate host lifecycle, layout, interaction, overlays, create palette, prompt-specific helpers, and diagnostics-ready exports.

### From Project Calendar

- remove legacy wrapper dependency
- replace `TryReadSelectedEventId` with typed state parsing

## Refactor completion signal

A refactor is only complete when the old location no longer owns the responsibility except for a temporary compatibility shim that is clearly marked for removal.
