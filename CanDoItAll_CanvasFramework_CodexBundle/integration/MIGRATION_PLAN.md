# Migration Plan

## Migration targets

### 1. Legacy graph wrapper retirement

**Current files**

- `src/CanDoItAll.Modules.Workbench/Components/ProjectStructureCanvas.razor#L1-L79` — Legacy project structure wrapper that still targets the old workbench JS runtime and should be retired after migration to the shared workbench system. Key symbols: OnAfterRenderAsync, CanDoItAll.workbenchCanvas.create, CanDoItAll.workbenchCanvas.update, CanDoItAll.workbenchCanvas.dispose.
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js#L1-L884` — Legacy graph and calendar runtime. Contains older canvas drawing helpers, hit testing, and duplicate calendar wrapper logic that should not continue as the strategic base. Key symbols: safeParse, getCanvasNodeBounds, drawRoundedRect, drawDiamond, drawHex, drawShield, ....

**Target path**

- Use `CanvasWorkbench` for all graph workbench scenarios.
- Route Project Structure through its new domain adapters.

### 2. Prompt Factory page cleanup

**Current files**

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090` — Selection graph builder and catalog behavior for the prompt factory canvas. Key symbols: BuildSelectionGraph, HandleCatalogContextActionAsync, HandleCatalogCreateActionAsync.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234` — Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. Key symbols: UndoAsync, RedoAsync, OnAfterRenderAsync.

**Target path**

- Keep the page as composition shell only.
- Move graph projection and history orchestration into adapters and infrastructure.

### 3. Project Calendar migration

**Current files**

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161` — Project calendar page that still uses the legacy calendar wrapper and manually parses selected event IDs from JSON view state. Key symbols: LoadAsync, HandleViewStateChangedAsync, TryReadSelectedEventId.
- `src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor#L1-L79` — Legacy project calendar wrapper using the old workbench JS runtime. This is the primary migration target for adopting CanvasCalendar. Key symbols: OnAfterRenderAsync, CanDoItAll.workbenchCalendar.create, CanDoItAll.workbenchCalendar.update, CanDoItAll.workbenchCalendar.dispose.
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js#L1-L884` — Legacy graph and calendar runtime. Contains older canvas drawing helpers, hit testing, and duplicate calendar wrapper logic that should not continue as the strategic base. Key symbols: safeParse, getCanvasNodeBounds, drawRoundedRect, drawDiamond, drawHex, drawShield, ....

**Target path**

- Replace `ProjectEventsCalendar` with `CanvasCalendar`.
- Introduce `ProjectCalendarAdapter` and `ProjectCalendarStateParser`.
- Remove manual `TryReadSelectedEventId` JSON probing.

## Migration guardrails

- Preserve current user-visible behavior first; improve internals second.
- Do not remove legacy code until the new path has parity and regression tests.
- Add temporary compatibility shims only when they reduce migration risk; mark them clearly for deletion.
