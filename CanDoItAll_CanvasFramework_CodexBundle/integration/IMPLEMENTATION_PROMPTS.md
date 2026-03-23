# Wave-Level Implementation Prompts

## Wave 1 prompt

Implement the shared host/foundation extraction for CanDoItAll. Reuse the current `CanvasWorkbench`, `CanvasCalendar`, `canvasWorkbenchInterop.js`, and `canvasCalendarInterop.js` code paths. Introduce the missing low-level shared boundaries (`CanvasSceneHost`, `SceneNodeModel`, `LayerStack`, `InvalidationScheduler`, `TextMeasureService`, `SerializationPersistencePack`, `JsInteropBridge`) without creating a second parallel workbench. Refactor the generic graph runtime so Prompt Factory-specific helper exports no longer live in the same module as the generic runtime. Update or add tests in `tests/CanDoItAll.Tests.Components`. Relevant files: `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs`, `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`, `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`, `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`, `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js`, `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs`, `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`.

## Wave 2 prompt

Implement the shared graph primitives and shell decomposition. Extract or add `NodeCardComposer`, `ConnectorPathPrimitive`, `ContextMenuHost`, `CreateActionPalette`, `InlineEditorComposer`, and `FloatingInspectorHost`. Preserve current `CanvasWorkbench` behavior while moving internal responsibilities into named components/modules. Do not create duplicate quick-create or context-menu systems outside the shared framework.

## Wave 3 prompt

Migrate Project Structure to domain adapters. Extract `MapCanvasNode`, `ResolveCreatePlacement`, and selection-border/group-frame generation out of `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`. Reuse `ProjectStructureCanvasCatalog` as the seed of `ProjectStructureActionCatalogAdapter`. Keep service calls in `ProjectWorkbenchService`.

## Wave 4 prompt

Migrate Prompt Factory to domain adapters and shared history infrastructure. Extract `BuildCanvasNodes`, `BuildCanvasLinks`, and `BuildSelectionGraph` from the page files. Promote the current history logic from `PromptFactoryPage.History.cs` into `CommandHistoryStore` + `PromptFactoryUndoRedoAdapter`. Move floating-inspector and shortcut helpers out of generic runtime modules.

## Wave 5 prompt

Migrate Project Calendar to `CanvasCalendar`. Replace `ProjectEventsCalendar` usage in `ProjectCalendarPage.razor` with a typed `ProjectCalendarAdapter` and `ProjectCalendarStateParser`. Remove manual raw-JSON selected-event parsing. Keep behavior parity first; do not rewrite the specialized calendar renderer unless required for wrapper compatibility.

## Wave 6 prompt

Implement the advanced interaction and future-readiness components: snapping, transform handles, connector anchors, clipboard, minimap, diagnostics, tooltip/popover host, validation overlays, and recommendation overlay. Build them on top of the shared foundation created in earlier waves. Do not introduce page-local hacks for these capabilities.
