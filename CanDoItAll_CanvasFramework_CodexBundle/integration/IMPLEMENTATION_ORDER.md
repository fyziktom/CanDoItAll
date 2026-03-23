# Implementation Order

## Recommended wave order

1. Wave 1 — host/foundation/interop/state extraction
2. Wave 2 — shared graph primitives and shell decomposition
3. Wave 3 — Project Structure adapter migration
4. Wave 4 — Prompt Factory adapter migration
5. Wave 5 — Project Calendar migration to CanvasCalendar
6. Wave 6 — advanced overlays, snapping, clipboard, minimap, diagnostics, recommendation UX

## File-by-file focus order

1. `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs`
2. `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
3. `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`
4. `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`
5. `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js`
6. `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
7. `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
8. `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs`
9. `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs`
10. `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
11. Legacy wrapper removal targets only after parity is reached:
    - `src/CanDoItAll.Modules.Workbench/Components/ProjectStructureCanvas.razor`
    - `src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor`
    - `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js`

## Why this order works

- The shared contracts and runtime shape influence every downstream page.
- Project Structure and Prompt Factory both depend on the graph-workbench family, so that family must stabilize first.
- Project Calendar migration should happen only after host/interop conventions are clean enough to avoid migrating to a moving target.
- Advanced overlays and interactions should land only when selection, hit testing, and persistence are explicit and reusable.
