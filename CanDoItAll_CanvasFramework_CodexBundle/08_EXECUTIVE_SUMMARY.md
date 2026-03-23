# Executive Summary

## High-level findings

1. The repository already has a **strong shared graph-workbench shell** in `CanvasWorkbench` and `CanvasWorkbenchStage`.
2. The repository also has the **correct first-step shared calendar wrapper** in `CanvasCalendar`.
3. The biggest structural problem is not missing visuals — it is missing **explicit low-level framework boundaries** plus **page-level graph logic leakage**.
4. The correct path is **progressive extraction and hardening**, not a greenfield replacement.

## Most critical architectural problems

- No explicit scene-graph abstraction behind the shared graph workbench
- Generic shared graph runtime is mixed with Prompt Factory-specific helpers
- Project Structure and Prompt Factory still build domain graph projection inside page files
- Legacy workbench wrapper/runtime still exists in parallel
- Project Calendar has not been migrated to the shared CanvasCalendar wrapper
- No shared text-measurement and truncation service

## Most important new shared components to add or extract

- CanvasSceneHost
- SceneNodeModel
- LayerStack
- InvalidationScheduler
- TextMeasureService
- NodeCardComposer
- ConnectorPathPrimitive
- HitTestService
- SnapGuideSystem
- ClipboardBridge
- MinimapOverview
- DiagnosticsOverlay

## Most important refactors

- Split `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js` into concern-specific modules while preserving the current shared workbench API surface.
- Extract `MapCanvasNode`, `ResolveCreatePlacement`, and selection-border generation out of `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`.
- Extract `BuildCanvasNodes`, `BuildCanvasLinks`, `BuildSelectionGraph`, and page-local history behavior out of `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage*.cs` files.
- Migrate `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor` from `ProjectEventsCalendar` to `CanvasCalendar` through `ProjectCalendarAdapter`.
- Retire `src/CanDoItAll.Modules.Workbench/Components/ProjectStructureCanvas.razor`, `src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor`, and the legacy `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js` path after migration.

## QA / review status

- Current-state analysis: **completed**
- Target architecture: **completed**
- Full component inventory: **completed**
- Per-component implementation folders: **completed**
- Integration bundle: **completed**
- QA/UX/UI/architecture review: **completed**
- Future-feature simulation: **completed**
- Final ZIP-ready bundle: **completed**

## Objective limitations that still remain

- The bundle is based on source-code analysis rather than a live running app session, so some visual runtime nuances should still be smoke-tested after implementation.
- The calendar runtime remains specialized and monolithic internally; the bundle deliberately recommends wrapper-first migration rather than an immediate full rewrite.
- The exact final theming palette remains a product/design decision; the bundle defines the technical boundary for theming rather than inventing a final palette.

## Final decision statement

The CanDoItAll shared canvas framework should be built by **strengthening the existing shared workbench and shared calendar wrapper, adding the missing low-level primitives and interaction systems, extracting domain adapters from pages, and removing the remaining legacy wrapper path**.
