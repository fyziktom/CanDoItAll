# Current-State Analysis

## Scope of analysis

The analysis focused on the canvas-based or canvas-style surfaces that matter for the requested framework target:

- Project Structure graph editor
- Prompt Factory graph editor
- Project Calendar and the emerging shared calendar wrapper
- Existing shared graph workbench infrastructure in `CanDoItAll.ComponentKit`
- Legacy workbench wrappers/runtime that still remain in the repo and must influence the migration plan

## Reality check: what the current system actually is

The repository does **not** contain one uniform low-level canvas framework today.

Instead, it contains **three related but different layers of reality**:

1. A **shared graph-workbench family** in `CanDoItAll.ComponentKit` built around `CanvasWorkbench`, backed by a large DOM/SVG/JS runtime with canvas-style interactions.
2. A **shared calendar wrapper** in `CanDoItAll.ComponentKit` built around `CanvasCalendar`, backed by a specialized monolithic calendar JS engine.
3. A **legacy workbench wrapper/runtime** in `CanDoItAll.Modules.Workbench` that still powers old graph/calendar integration surfaces and duplicates some capabilities.

That distinction matters, because the target architecture should **reuse and harden the shared graph-workbench family** while **migrating the calendar to the shared wrapper**, not force everything into one false "all canvases are the same engine" model.

## Relevant surfaces and how they currently behave


## Shared graph workbench foundation

**Primary surface:** `CanvasWorkbench + CanvasWorkbenchStage`

**Purpose:** Provide a reusable graph-style work surface with shell chrome, typed surface DTOs, zoom/pan, context actions, group frames, multi-select, and inspector integration.

**Main use cases**

- Project Structure graph editing.
- Prompt Factory graph editing.
- Future graph-like workbenches built on the same shell.

**User interactions**

- Toolbar actions, fit view, focus primary node, maximize toggle.
- Pan, wheel zoom, context menu, quick create, inline note editor, marquee selection, multi-node drag, frame drag.
- Selection state publication, node movement callbacks, state persistence callbacks.

**Visual elements**

- Rich HTML node cards inside a transformed scene host.
- SVG connectors and group frames.
- Canvas/workbench-style chrome, right inspector, and lower panels supplied by the stage shell.

**What should be shared**

- Canvas host lifecycle
- Viewport controller
- Context menu host
- Node card composer
- Selection model
- Group frame overlay
- Create action palette

**What stays canvas/domain specific**

- No domain-specific rules by itself; domain adapters should provide node shapes, actions, and inspector coupling.

**Key code references**

- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340` — Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. Key symbols: CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572` — Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82` — Shared shell used by Project Structure and Prompt Factory for eyebrow/title copy, stats, canvas slot, inspector slot, and supporting panels. Key symbols: CanvasWorkbenchStage.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.

## Project Structure canvas

**Primary surface:** `ProjectStructurePage`

**Purpose:** Visualize and edit project object hierarchy, links, markers, progress, and create flows in a graph workbench.

**Main use cases**

- Select and inspect project objects.
- Create child or sibling objects in context.
- Move nodes and persist coordinates.
- Manage markers, priorities, progress, and selection borders.

**User interactions**

- Node selection and multi-selection.
- Node move and persistence.
- Context and create actions.
- Inspector-driven edits and selection-border actions.

**Visual elements**

- Object cards with type, title, and status metadata.
- Links between project objects.
- Selection border/group frame overlay generated from page logic.

**What should be shared**

- Graph adapter
- Action catalog adapter
- Placement policy
- Validation overlay
- Shared node card templates

**What stays canvas/domain specific**

- Domain object taxonomy and create definitions.
- Project-specific progress/marker/priority commands.
- Project-specific graph-health and outline panels.

**Key code references**

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326` — Domain action catalog and label resolver for Project Structure create flows and inspector create groups. Key symbols: ResolveNodeLabel, BuildMenuCreateActions, BuildInspectorCreateGroups, TryResolveCreateDefinition.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806` — Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. Key symbols: ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572` — Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync, ....

## Prompt Factory canvas

**Primary surface:** `PromptFactoryPage`

**Purpose:** Project prompt-session state into a graph editor with selection graph, branch lanes, attachments, and run-step context.

**Main use cases**

- Inspect and edit prompt-session structure.
- Navigate setup, blueprint, flow, components, inputs, attachments, and run nodes.
- Use contextual create and action flows.
- Undo/redo prompt edits and persist canvas UI state.

**User interactions**

- Selection and node open callbacks.
- Contextual actions and create flows.
- Undo/redo toolbar and keyboard shortcuts.
- Floating inspector sync.

**Visual elements**

- Session root and typed graph node cards.
- Branch lanes and attachment nodes.
- Floating inspector overlay in addition to stage inspector patterns.

**What should be shared**

- Session graph adapter
- Catalog toolbox
- Undo/redo adapter
- Branch lane component
- Attachment node component
- Recommendation overlay

**What stays canvas/domain specific**

- Prompt-session domain topology and run semantics.
- Domain build/export/send workflows.
- Selection graph composition logic unique to prompt authoring.

**Key code references**

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090` — Selection graph builder and catalog behavior for the prompt factory canvas. Key symbols: BuildSelectionGraph, HandleCatalogContextActionAsync, HandleCatalogCreateActionAsync.
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645` — Prompt Factory domain create/action catalog used to populate canvas context actions and create menus. Key symbols: BuildSessionContextActions, BuildSelectionContextActions, BuildComponentNodeActions, BuildInputNodeActions.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234` — Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. Key symbols: UndoAsync, RedoAsync, OnAfterRenderAsync.
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536` — Prompt Factory domain models, including run node summaries, attachment summaries, setup profile, and editor state persisted with CanvasUiStateJson. Key symbols: PromptRunNodeSummary, PromptSessionAttachmentSummary, PromptSessionSetupProfile, PromptFactoryEditorModel.
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715` — Prompt Factory domain service handling recommendations, state persistence, branching, build/export/send flows, and canvas UI-state persistence. Key symbols: GetRecommendedBlockIdsAsync, SaveSessionStateAsync, SaveCanvasUiStateAsync, BranchNodeAsync, UpdateNodeAsync, SetNodeStateAsync, ....

## Shared calendar wrapper

**Primary surface:** `CanvasCalendar`

**Purpose:** Wrap the specialized calendar runtime in a typed Blazor component with save/delete/export and selection/state callbacks.

**Main use cases**

- Host a full-widget calendar inside Blazor.
- Provide typed event operations to C# services.
- Serve as the migration target for Project Calendar.

**User interactions**

- Select events and emit typed selection data.
- Save/delete events and perform playlist operations.
- Emit view-state updates and export requests.

**Visual elements**

- Owned by the specialized JS widget: timed views, month view, year view, side panel, list, modal editor, export affordances.

**What should be shared**

- Calendar host lifecycle
- CRUD bridge
- Time-grid renderer boundary
- Selection panel boundary
- Export menu boundary

**What stays canvas/domain specific**

- Calendar is a sibling runtime, not a graph scene graph.

**Key code references**

- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223` — Defines the typed wrapper contract for the shared calendar runtime: surface, event model, playlist models, operation context, selection/state callbacks, and export request. Key symbols: CanvasCalendarSurface, CanvasCalendarEvent, CanvasCalendarPlaylist, CanvasCalendarConnectedEvent, CanvasCalendarChecklistRow, CanvasCalendarOperationContext, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258` — Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335` — Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. Key symbols: safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md#L1-L186` — Existing design note arguing for a full-widget Blazor wrapper around the calendar runtime before deeper rewrite. Key symbols: Core recommendation, Target architecture.

## Project Calendar legacy integration

**Primary surface:** `ProjectCalendarPage + ProjectEventsCalendar + legacy workbenchInterop.js`

**Purpose:** Show project events, persist view state, and select events through the old wrapper/runtime.

**Main use cases**

- View project events and current calendar state.
- Persist view state.
- Select a current event for side panel details.

**User interactions**

- Legacy calendar callbacks.
- Manual parsing of selected event ID from view-state JSON.

**Visual elements**

- Calendar UI provided by legacy wrapper/runtime rather than shared CanvasCalendar.

**What should be shared**

- ProjectCalendarAdapter
- ProjectCalendarStateParser
- CanvasCalendarHost migration

**What stays canvas/domain specific**

- Project-specific event mapping and persistence service.

**Key code references**

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161` — Project calendar page that still uses the legacy calendar wrapper and manually parses selected event IDs from JSON view state. Key symbols: LoadAsync, HandleViewStateChangedAsync, TryReadSelectedEventId.
- `src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor#L1-L79` — Legacy project calendar wrapper using the old workbench JS runtime. This is the primary migration target for adopting CanvasCalendar. Key symbols: OnAfterRenderAsync, CanDoItAll.workbenchCalendar.create, CanDoItAll.workbenchCalendar.update, CanDoItAll.workbenchCalendar.dispose.
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js#L1-L884` — Legacy graph and calendar runtime. Contains older canvas drawing helpers, hit testing, and duplicate calendar wrapper logic that should not continue as the strategic base. Key symbols: safeParse, getCanvasNodeBounds, drawRoundedRect, drawDiamond, drawHex, drawShield, ....
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806` — Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. Key symbols: ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest, ....


## Existing shared building blocks already in the repository

### Shared graph-workbench assets

- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340` — Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. Key symbols: CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572` — Reusable Blazor host for the shared workbench canvas, including toolbar chrome, maximize/focus/fit interactions, JS interop lifecycle, and typed callbacks. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnNodesMoved, OnStateChanged, ToggleMaximizeAsync, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82` — Shared shell used by Project Structure and Prompt Factory for eyebrow/title copy, stats, canvas slot, inspector slot, and supporting panels. Key symbols: CanvasWorkbenchStage.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.

### Shared calendar assets

- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223` — Defines the typed wrapper contract for the shared calendar runtime: surface, event model, playlist models, operation context, selection/state callbacks, and export request. Key symbols: CanvasCalendarSurface, CanvasCalendarEvent, CanvasCalendarPlaylist, CanvasCalendarConnectedEvent, CanvasCalendarChecklistRow, CanvasCalendarOperationContext, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258` — Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335` — Thin bridge between Blazor and the legacy/shared calendar engine. Handles state serialization, typed callback payloads, and JS widget lifecycle. Key symbols: safeParse, buildContext, buildStateJson, parseViewState, normalizeCalendarEventForDotNet, emitState, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....

### Domain pages and services that currently shape the canvases

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326` — Domain action catalog and label resolver for Project Structure create flows and inspector create groups. Key symbols: ResolveNodeLabel, BuildMenuCreateActions, BuildInspectorCreateGroups, TryResolveCreateDefinition.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806` — Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. Key symbols: ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090` — Selection graph builder and catalog behavior for the prompt factory canvas. Key symbols: BuildSelectionGraph, HandleCatalogContextActionAsync, HandleCatalogCreateActionAsync.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37` — Floating inspector docking logic used by the prompt factory canvas. Key symbols: DockCanvasInspectorAsync, SyncFloatingInspectorAsync.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234` — Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. Key symbols: UndoAsync, RedoAsync, OnAfterRenderAsync.
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645` — Prompt Factory domain create/action catalog used to populate canvas context actions and create menus. Key symbols: BuildSessionContextActions, BuildSelectionContextActions, BuildComponentNodeActions, BuildInputNodeActions.
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536` — Prompt Factory domain models, including run node summaries, attachment summaries, setup profile, and editor state persisted with CanvasUiStateJson. Key symbols: PromptRunNodeSummary, PromptSessionAttachmentSummary, PromptSessionSetupProfile, PromptFactoryEditorModel.
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715` — Prompt Factory domain service handling recommendations, state persistence, branching, build/export/send flows, and canvas UI-state persistence. Key symbols: GetRecommendedBlockIdsAsync, SaveSessionStateAsync, SaveCanvasUiStateAsync, BranchNodeAsync, UpdateNodeAsync, SetNodeStateAsync, ....
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161` — Project calendar page that still uses the legacy calendar wrapper and manually parses selected event IDs from JSON view state. Key symbols: LoadAsync, HandleViewStateChangedAsync, TryReadSelectedEventId.

### Existing tests and prior internal design notes

- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs#L1-L123` — Current component tests touching project structure page rendering and action exposure. Key symbols: Renders shared structure page, Prompt flow nodes expose Wizard navigation.
- `tests/CanDoItAll.Tests.Components/PromptFactoryPageTests.cs#L1-L64` — Current component tests touching prompt factory undo/redo controls and preview modal behavior. Key symbols: Renders undo/redo controls, Preview query opens built prompt modal.
- `docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203` — Existing internal analysis of reference workbench behavior and gaps. Key symbols: Reference capability inventory, Page shell and layout, Canvas host and chrome.
- `docs/canvases-improvements/02-shared-canvas-system-spec.md#L1-L430` — Existing shared canvas system specification that already separates Blazor-owned and JS-owned responsibilities. Key symbols: Shared architecture, JavaScript owns, Blazor owns.
- `docs/canvases-improvements/04-implementation-plan.md#L1-L334` — Existing implementation plan for the shared canvas direction. Helpful for sequencing and validating the migration roadmap. Key symbols: Phase 1: Build the shared canvas foundation.
- `docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md#L1-L186` — Existing design note arguing for a full-widget Blazor wrapper around the calendar runtime before deeper rewrite. Key symbols: Core recommendation, Target architecture.
- `docs/ui-shared-components/recommendations/missing-components.md#L1-L241` — Existing recommendation list that already calls out modal, tooltip, popover, and other shared UI gaps relevant to canvas work. Key symbols: Real tooltip / popover / context-menu system.

## Reuse vs duplication matrix

| Area | Current shared asset | Current duplicate or leakage | Assessment |
| --- | --- | --- | --- |
| Graph shell | CanvasWorkbench / CanvasWorkbenchStage | ProjectStructurePage and PromptFactoryPage still own domain graph projection | Keep shared shell; extract adapters from pages. |
| Graph runtime | canvasWorkbenchInterop.js | Prompt Factory floating inspector helper mixed into generic JS | Split by concern; keep runtime as strategic base. |
| Graph legacy | Shared workbench exists | ProjectStructureCanvas.razor + old workbenchInterop.js still exist | Retire legacy graph wrapper/runtime after migration. |
| Calendar wrapper | CanvasCalendar + canvasCalendarInterop.js | ProjectCalendarPage still uses ProjectEventsCalendar + old workbenchInterop.js | Migrate Project Calendar to shared wrapper. |
| Selection border/group frame | CanvasWorkbenchGroupFrame + renderGroupFrames | ProjectStructurePage still creates/clears frames in page methods | Move into shared overlay or domain adapter. |
| Create menus | CanvasWorkbenchAction + openCreateComposer/showContextMenu | Page-level action tree assembly and domain catalogs | Keep shared UI; formalize domain catalog adapters. |
| Undo/redo | No shared infra | PromptFactoryPage.History.cs page-local implementation | Promote to shared CommandHistoryStore + domain adapter. |
| State persistence | Surface/view state already persisted in services | ProjectCalendarPage manual JSON probing | Create typed persistence pack and state parser. |

## Where responsibilities are currently mixed in the wrong place

### Graph rendering, scene state, and page domain logic

- `ProjectStructurePage.razor#L344-L1091` mixes:
  - graph projection (`MapCanvasNode`)
  - create placement (`ResolveCreatePlacement`)
  - selection-border/group-frame generation (`CreateSelectionBorderAsync`, `ClearSelectionBordersAsync`)
  - persistence callbacks (`HandleNodesMovedAsync`, `HandleCanvasStateChangedAsync`)
- `PromptFactoryPage.razor#L1994-L2570` mixes:
  - graph projection (`BuildCanvasNodes`, `BuildCanvasLinks`)
  - state persistence (`PersistCanvasUiStateAsync`)
  - domain action execution (`HandleCanvasContextActionAsync`, `HandleCanvasCreateActionAsync`)
- `PromptFactoryPage.Catalog.cs#L71-L497` holds selection-graph assembly that should move behind a dedicated domain adapter boundary.

### Shared runtime plus page-specific UI helpers in one JS file

- `canvasWorkbenchInterop.js#L1-L60` exposes prompt-factory-specific helper exports next to the generic workbench runtime.
- `canvasWorkbenchInterop.js#L206-L4099` contains host lifecycle, layout, rendering, overlays, create composer, inline editor, pan/zoom, selection, and frame drag in one file.
- The correct next step is **module decomposition**, not a ground-up replacement.

### Shared calendar wrapper exists, but page migration is unfinished

- `CanvasCalendar.razor#L1-L258` and `canvasCalendarInterop.js#L13-L335` already implement the strategic wrapper shape.
- `ProjectCalendarPage.razor#L1-L161` still uses `ProjectEventsCalendar.razor#L1-L79`.
- `ProjectCalendarPage.razor#L143-L160` manually parses `selectedEventId` from raw JSON, which is an avoidable fragility.

### Legacy graph/calendar runtime keeps parallel debt alive

- `workbenchInterop.js#L1-L884` still owns:
  - old graph hit-testing and shape drawing
  - old calendar wrapper behavior
- As long as that file remains strategically important, Codex agents can accidentally re-extend the wrong foundation.

## Current strengths worth preserving

- Typed shared DTO contracts already exist for graph and calendar surfaces.
- CanvasWorkbench already provides a polished shell with fit/focus/maximize, create actions, context menus, notes, group frames, and multi-select basics.
- CanvasWorkbenchStage already unifies the page shell pattern across Project Structure and Prompt Factory.
- CanvasCalendar already follows the right wrapper idea: keep the dense rendering engine in JS, expose typed operations to C#.
- ProjectStructureCanvasCatalog and PromptFactoryCanvasCatalog are valuable seeds for domain action adapters.
- ProjectWorkbenchService and PromptFactoryService already persist enough state to support a mature framework direction.
- The repo already contains internal docs that point toward shared-canvas reuse rather than further duplication.

## Concrete architectural problems


### 1. No explicit scene-graph abstraction behind the shared graph workbench

CanvasWorkbenchSurface is a useful DTO layer, but the runtime lacks a formal internal scene-node hierarchy, explicit layers, and invalidation model. This makes advanced features harder to add cleanly.

Key references:

- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340` — Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. Key symbols: CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `docs/canvases-improvements/02-shared-canvas-system-spec.md#L1-L430` — Existing shared canvas system specification that already separates Blazor-owned and JS-owned responsibilities. Key symbols: Shared architecture, JavaScript owns, Blazor owns.

### 2. Generic shared graph runtime is mixed with Prompt Factory-specific helpers

canvasWorkbenchInterop.js contains mountFloatingInspector and prompt-factory shortcut helpers at the top level, which mixes generic workbench runtime responsibilities with page-specific behavior.

Key references:

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37` — Floating inspector docking logic used by the prompt factory canvas. Key symbols: DockCanvasInspectorAsync, SyncFloatingInspectorAsync.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234` — Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. Key symbols: UndoAsync, RedoAsync, OnAfterRenderAsync.

### 3. Project Structure and Prompt Factory still build domain graph projection inside page files

MapCanvasNode, ResolveCreatePlacement, BuildCanvasNodes, BuildCanvasLinks, and BuildSelectionGraph live in page code instead of dedicated domain adapters.

Key references:

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090` — Selection graph builder and catalog behavior for the prompt factory canvas. Key symbols: BuildSelectionGraph, HandleCatalogContextActionAsync, HandleCatalogCreateActionAsync.

### 4. Legacy workbench wrapper/runtime still exists in parallel

ProjectStructureCanvas.razor, ProjectEventsCalendar.razor, and the old workbenchInterop.js keep an alternative canvas/calendar stack alive. That duplication is a long-term maintenance and migration risk.

Key references:

- `src/CanDoItAll.Modules.Workbench/Components/ProjectStructureCanvas.razor#L1-L79` — Legacy project structure wrapper that still targets the old workbench JS runtime and should be retired after migration to the shared workbench system. Key symbols: OnAfterRenderAsync, CanDoItAll.workbenchCanvas.create, CanDoItAll.workbenchCanvas.update, CanDoItAll.workbenchCanvas.dispose.
- `src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor#L1-L79` — Legacy project calendar wrapper using the old workbench JS runtime. This is the primary migration target for adopting CanvasCalendar. Key symbols: OnAfterRenderAsync, CanDoItAll.workbenchCalendar.create, CanDoItAll.workbenchCalendar.update, CanDoItAll.workbenchCalendar.dispose.
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js#L1-L884` — Legacy graph and calendar runtime. Contains older canvas drawing helpers, hit testing, and duplicate calendar wrapper logic that should not continue as the strategic base. Key symbols: safeParse, getCanvasNodeBounds, drawRoundedRect, drawDiamond, drawHex, drawShield, ....

### 5. Project Calendar has not been migrated to the shared CanvasCalendar wrapper

ProjectCalendarPage still targets the legacy wrapper and even manually parses selected event IDs from persisted JSON view state.

Key references:

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161` — Project calendar page that still uses the legacy calendar wrapper and manually parses selected event IDs from JSON view state. Key symbols: LoadAsync, HandleViewStateChangedAsync, TryReadSelectedEventId.
- `src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor#L1-L79` — Legacy project calendar wrapper using the old workbench JS runtime. This is the primary migration target for adopting CanvasCalendar. Key symbols: OnAfterRenderAsync, CanDoItAll.workbenchCalendar.create, CanDoItAll.workbenchCalendar.update, CanDoItAll.workbenchCalendar.dispose.
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258` — Reusable Blazor wrapper around the calendar runtime with typed callbacks, export hook, and JS interop lifecycle. Key symbols: OnParametersSet, OnAfterRenderAsync, OnSelectionChanged, OnStateChanged, ExportAsync.

### 6. No shared text-measurement and truncation service

Text-heavy nodes, menus, and events rely on DOM or internal widget behavior rather than a shared measurement contract. This will become a source of inconsistency as more components arrive.

Key references:

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.

### 7. No formal snapping, transform, minimap, clipboard, or diagnostics subsystems

These are all explicitly needed by the target direction and are absent or only implicit in the current implementation.

Key references:

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203` — Existing internal analysis of reference workbench behavior and gaps. Key symbols: Reference capability inventory, Page shell and layout, Canvas host and chrome.
- `docs/canvases-improvements/04-implementation-plan.md#L1-L334` — Existing implementation plan for the shared canvas direction. Helpful for sequencing and validating the migration roadmap. Key symbols: Phase 1: Build the shared canvas foundation.

### 8. Undo/redo is page-local instead of shared infrastructure

Prompt Factory already needs history, but it is implemented inside PromptFactoryPage.History.cs. Project Structure and future graph editors will need the same capability.

Key references:

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234` — Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. Key symbols: UndoAsync, RedoAsync, OnAfterRenderAsync.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....

### 9. The specialized calendar engine is monolithic and style-owning

zy-canvas-calendar.js already contains rendering, panel, editor, export, and support flows, which is fine for a first wrapper but risky as a long-term permanent architecture without explicit boundaries.

Key references:

- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md#L1-L186` — Existing design note arguing for a full-widget Blazor wrapper around the calendar runtime before deeper rewrite. Key symbols: Core recommendation, Target architecture.

### 10. Testing is concentrated at page level with almost no runtime-service validation

Existing tests cover only a small slice of rendering and toolbar behavior. There are no targeted tests for layout, hit testing, selection, persistence, snapping, or diagnostics.

Key references:

- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs#L1-L123` — Current component tests touching project structure page rendering and action exposure. Key symbols: Renders shared structure page, Prompt flow nodes expose Wizard navigation.
- `tests/CanDoItAll.Tests.Components/PromptFactoryPageTests.cs#L1-L64` — Current component tests touching prompt factory undo/redo controls and preview modal behavior. Key symbols: Renders undo/redo controls, Preview query opens built prompt modal.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....


## Missing low-level primitives and subsystems

The current codebase is already useful, but it still lacks clear first-class implementations for the following foundation pieces:

- Scene node model and explicit layer stack
- Redraw invalidation scheduler
- Shared text measurement and truncation service
- Standalone connector primitive and connector-anchor system
- Shared snapping and alignment guides
- Transform handles and generic transform interaction layer
- Clipboard bridge
- Minimap overview
- Diagnostics/profiling/test-hook overlay
- Accessibility fallback mirror layer

## Shared-vs-specific conclusion

The correct split is:

- Keep the **graph workbench shell, scene contracts, interactions, overlays, and visual primitives** in the shared framework.
- Keep the **calendar wrapper, CRUD bridge, and wrapper lifecycle** shared, but treat the dense calendar renderer as a specialized sibling runtime rather than forcing it into the graph scene model.
- Move **Project Structure** and **Prompt Factory** graph projection, create rules, placement policies, and validation semantics into domain adapters.
- Migrate **Project Calendar** to `CanvasCalendar` via a domain adapter and typed state parser, then retire the legacy wrapper path.

## Final current-state verdict

The repository already has the beginnings of a strong shared canvas system, but it is still in the **"shared shell + monolithic runtime + page-level adapters leaking everywhere"** stage.

The work should therefore focus on:

1. Decomposing the existing shared graph runtime into clearer subsystem boundaries.
2. Introducing missing low-level framework pieces that make advanced interactions sustainable.
3. Extracting page-level mapping/action/history/placement logic into explicit adapters and services.
4. Finishing the calendar migration to the shared wrapper and quarantining the legacy runtime.
5. Adding diagnostics, validation, and future-feature readiness before the framework expands further.
