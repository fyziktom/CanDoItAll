# Component Inventory

## Reading guide

- **Status**
  - `exists` — already present as a named reusable component and should be hardened, not replaced
  - `partial` — significant logic exists, but it is mixed, monolithic, or page-owned
  - `missing` — a first-class component boundary does not exist yet
- **Priority**
  - `P0` — required to stabilize the framework and current migrations
  - `P1` — strongly recommended next wave
  - `P2` — near-term future readiness or advanced capability
- **Level**
  - `low-level` — primitive, infrastructure, or subsystem
  - `high-level` — composed UI component or domain adapter

## Inventory summary by status

| Status | Count | Meaning |
| --- | --- | --- |
| exists | 2 | Already reusable and strategically correct. |
| partial | 41 | Present in code but needs extraction/decomposition/hardening. |
| missing | 19 | Must be added as a first-class component. |

## Inventory summary by priority

| Priority | Count | Meaning |
| --- | --- | --- |
| P0 | 27 | Foundation and migration critical. |
| P1 | 26 | High-value next wave. |
| P2 | 9 | Near-term extensibility / advanced capability. |

## Inventory summary by scope

| Scope | Count | Meaning |
| --- | --- | --- |
| shared | 50 | Belongs in the reusable framework layer. |
| domain-specific | 12 | Belongs in module adapters or domain overlays. |

## Already present and worth preserving

- CanvasWorkbenchShell
- CanvasWorkbenchStageShell

## Present but only partially isolated

- CanvasSceneHost
- GridBackdrop
- LayoutEngine
- CanvasThemeTokenPack
- JsInteropBridge
- SerializationPersistencePack
- CommandHistoryStore
- TextBlockPrimitive
- IconGlyphPrimitive
- ChipBadgePrimitive
- ImagePrimitive
- ContainerPrimitive
- ConnectorPathPrimitive
- ViewportController
- HitTestService
- SelectionModel
- HoverFocusRouter
- DragDropController
- MarqueeSelectionOverlay
- KeyboardShortcutRouter
- NodeCardComposer
- GroupFrameOverlay
- CreateActionPalette
- ContextMenuHost
- InlineEditorComposer
- FloatingInspectorHost
- CanvasCalendarHost
- CalendarTimeGridRenderer
- CalendarMiniMonthNavigator
- CalendarSelectionPanel
- CalendarEventEditorModal
- CalendarCrudBridge
- CalendarExportMenu
- ProjectStructureGraphAdapter
- ProjectStructureActionCatalogAdapter
- ProjectStructurePlacementPolicy
- PromptFactorySessionGraphAdapter
- PromptFactoryCatalogToolbox
- PromptRunBranchLane
- PromptSessionAttachmentNode
- PromptFactoryUndoRedoAdapter

## Definitely missing today

- SceneNodeModel
- LayerStack
- InvalidationScheduler
- TextMeasureService
- DiagnosticsOverlay
- SnapGuideSystem
- TransformHandlesOverlay
- ConnectorAnchorOverlay
- ClipboardBridge
- AccessibilityMirrorLayer
- AnimationTimeline
- TooltipPopoverHost
- EmptyStateOverlay
- SkeletonStateOverlay
- MinimapOverview
- ProjectCalendarAdapter
- ProjectStructureValidationOverlay
- RecommendationOverlay
- ProjectCalendarStateParser

## Very likely needed soon

- SnapGuideSystem
- TransformHandlesOverlay
- ConnectorAnchorOverlay
- ClipboardBridge
- TooltipPopoverHost
- MinimapOverview
- DiagnosticsOverlay
- RecommendationOverlay

## Detailed inventory by category


## Basic primitives

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ChipBadgePrimitive | partial | P1 | low-level | shared | none | Wave 2 | CanvasWorkbenchChip exists as data but not as a reusable rendering primitive across all surfaces. | Priority, status, and marker pills in Project Structure cards.; State badges and metadata tags in Prompt Factory nodes. |
| IconGlyphPrimitive | partial | P1 | low-level | shared | none | Wave 2 | Icons are currently embedded ad hoc in cards, chips, and menu entries instead of being a shared primitive. | Node type icons in Project Structure and Prompt Factory.; Toolbar and context menu icons in the workbench shell. |

## Text components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TextMeasureService | missing | P0 | low-level | shared | required | Wave 1 | Text is a central element of every current canvas surface, but measurement rules live implicitly in DOM or specialized JS code. A shared service is required for consistent card sizing and truncation behavior. | Measure node titles and subtitles before final card layout.; Apply consistent multi-line wrapping and ellipsis rules to calendar events and graph cards. |
| TextBlockPrimitive | partial | P0 | low-level | shared | partial | Wave 2 | Node cards, menus, chips, and calendar events all render text but not through one reusable primitive. | Render project node titles, summaries, and metadata rows.; Render prompt node labels, status captions, and context menus. |

## Image components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ImagePrimitive | partial | P1 | low-level | shared | partial | Wave 2 | Image usage exists in node cards and inspectors, but no shared canvas-friendly image primitive exists yet. | Prompt session attachment previews on the canvas.; Future project structure image or cover nodes. |

## Containers

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ContainerPrimitive | partial | P0 | low-level | shared | partial | Wave 2 | Node cards, inspector panels, selection frames, and future popovers all need consistent container rules beyond raw HTML divs. | Project Structure node cards and group frames.; Prompt Factory session, component, branch, and attachment nodes. |
| NodeCardComposer | partial | P0 | high-level | shared | required | Wave 2 | Node rendering is currently hardwired inside the shared JS runtime and page data mappers. It should become a reusable component composition layer. | Project Structure object cards with type, title, status, priority, and marker metadata.; Prompt Factory session, component, input, branch, and run-step cards. |

## Interactive components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| DragDropController | partial | P0 | low-level | shared | required | Wave 1 | Drag logic exists but is embedded in the shared runtime and page-specific move persistence. It needs clearer ownership and drop-target extensibility. | Drag one or many nodes in the workbench.; Drag group frames or future connector handles. |
| HitTestService | partial | P0 | low-level | shared | required | Wave 1 | Hit logic exists implicitly via DOM events in the shared runtime and manually in the legacy workbench canvas; it should be unified and testable. | Detect pointer targets for selection, drag, context menus, and hover states.; Support future connector anchor handles and resize/rotate handles. |
| HoverFocusRouter | partial | P1 | low-level | shared | required | Wave 1 | Hover and focus are currently handled locally by DOM events and CSS. A shared router is needed to keep complex overlays and keyboard interactions coherent. | Highlight a hovered node while suppressing stale hover when a context menu opens.; Transfer focus between canvas node, inline editor, and floating inspector without losing semantic selection. |
| KeyboardShortcutRouter | partial | P1 | high-level | shared | required | Wave 1 | Prompt Factory already registers custom shortcuts while the workbench runtime also handles zoom/help keys. This should be formalized. | Undo/redo, fit view, zoom in/out, focus primary node, and open help overlay.; Context-sensitive shortcuts that change between graph editing and inline editing modes. |

## Connector and relationship components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ConnectorPathPrimitive | partial | P0 | low-level | shared | required | Wave 2 | Connectors are central to both graph canvases, but rendering and routing are embedded in the shared JS file and legacy workbench runtime instead of a dedicated primitive. | Project Structure parent/child and dependency connectors.; Prompt Factory setup-flow-component-input links and branch edges. |
| ConnectorAnchorOverlay | missing | P1 | high-level | shared | required | Wave 6 | Future connector authoring and routing improvements need explicit anchor visuals and hit regions, which do not exist yet. | Create or reroute dependencies in Project Structure.; Connect prompt flow nodes, branches, or future validation overlays in Prompt Factory. |

## Overlay, inspector, and helper components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ContextMenuHost | partial | P0 | high-level | shared | required | Wave 2 | A robust context menu exists in the workbench JS, but it is still embedded and mixed with create flow behaviors. It should be elevated into a reusable overlay component. | Project Structure node actions, create menus, and utility commands.; Prompt Factory node and session context actions. |
| FloatingInspectorHost | partial | P1 | high-level | shared | required | Wave 2 | Prompt Factory already has a floating inspector helper, but it is mixed into generic workbench JS and should become a first-class shared component. | Prompt Factory floating inspector docking and persistence.; Future compact inspector mode for smaller screens or multi-canvas workflows. |
| GroupFrameOverlay | partial | P1 | high-level | shared | required | Wave 2 | Group frames exist already as data and JS rendering, but their generation and persistence are partly page-specific. | Selection border creation in Project Structure.; Prompt Factory branch lanes or grouped workflow stages. |
| EmptyStateOverlay | missing | P2 | high-level | shared | none | Wave 6 | Empty surfaces will become more common as framework reuse grows; explicit empty-state handling is currently missing. | Show Project Structure onboarding for a blank project graph.; Show Prompt Factory guidance when no canvas nodes are yet projected. |
| SkeletonStateOverlay | missing | P2 | high-level | shared | none | Wave 6 | Canvas surfaces often show data-dependent content, but there is no shared skeleton strategy for them today. | Show a coherent loading frame while graph nodes are loading.; Mask small async refreshes of side panels without layout jumps. |
| TooltipPopoverHost | missing | P1 | high-level | shared | required | Wave 6 | Tooltip/popover support is explicitly missing in broader shared UI docs and is needed by upcoming canvas features. | Show full text for truncated labels.; Display validation guidance or recommendation details near nodes. |

## Advanced graphical components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GridBackdrop | partial | P1 | low-level | shared | required | Wave 1 | The current experience has spatial framing but no formal shared grid component that can support snapping, guides, zoom scaling, or future minimap rendering. | Show subtle grid lines for Project Structure placement.; Provide zoom-aware guide spacing in Prompt Factory. |
| AnimationTimeline | missing | P2 | low-level | shared | required | Wave 6 | The product wants wow effect and fluidity, but there is no shared animation layer. Without one, every new animation will become bespoke. | Animate fit-to-view or focus transitions.; Fade in selection overlays or guide lines. |

## Layout and navigation components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| LayoutEngine | partial | P0 | low-level | shared | required | Wave 1 | Layout logic exists today but is embedded in one JS file and page-specific placement code. It needs to become a first-class shared service. | Resolve manual positions, fallback auto positions, and content-measured card sizes.; Lay out Prompt Factory branch lanes and component stacks consistently. |
| ViewportController | partial | P0 | low-level | shared | required | Wave 1 | Viewport behavior exists today but is embedded inside the JS runtime and exposed piecemeal through host methods. | Pan and zoom the workbench with wheel, trackpad, keyboard, or toolbar controls.; Focus a primary node after selection or deep-link navigation. |
| CanvasWorkbenchShell | exists | P0 | high-level | shared | required | Wave 2 | This already exists and is the correct strategic starting point, but it needs internal decomposition so it can become the long-lived shell of the graph framework. | Project Structure graph editing.; Prompt Factory canvas editing. |
| CanvasWorkbenchStageShell | exists | P0 | high-level | shared | none | Wave 2 | This stage shell already unifies page composition language between Project Structure and Prompt Factory. | Render left canvas and right inspector layout.; Expose lower supporting panels and custom toolbar slots. |
| MinimapOverview | missing | P2 | high-level | shared | required | Wave 6 | Large scenes in Project Structure and Prompt Factory will become harder to navigate without an overview. The current framework has no such component. | Navigate large project structure graphs quickly.; Understand branch spread in Prompt Factory canvases. |

## Editing components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CreateActionPalette | partial | P0 | high-level | shared | required | Wave 2 | Create flows are central to both canvases and already partially shared through CanvasWorkbenchAction, but rendering and orchestration are still mixed with context menus and page logic. | Open quick-create from the workbench toolbar.; Open contextual create flows from node menus or inspector groups. |
| InlineEditorComposer | partial | P1 | high-level | shared | required | Wave 2 | The workbench already contains inline note composers and edit flows, but they are hardwired inside JS. They should become a reusable editor host. | Existing inline note create/edit in the workbench runtime.; Future quick rename or status edit flows directly on node cards. |
| ClipboardBridge | missing | P1 | high-level | shared | required | Wave 6 | Clipboard workflows are called out as a requirement and are currently unsupported by shared canvas components. | Duplicate a selected prompt subgraph with preserved relative positions.; Copy Project Structure nodes or groups into another location or project. |

## Selection and transform components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SelectionModel | partial | P0 | low-level | shared | partial | Wave 1 | Selection state is already central but split across JS state, C# DTOs, and page-level logic. It needs a shared authoritative model. | Track primary node and selected node IDs in Project Structure and Prompt Factory.; Drive inspector content, toolbar enablement, and selection frame rendering. |
| MarqueeSelectionOverlay | partial | P1 | high-level | shared | required | Wave 6 | Marquee selection already exists in the shared runtime but is not isolated as a reusable overlay component with explicit policy knobs. | Alt-drag selection in the shared workbench.; Potential future touch-lasso or box-select gestures. |
| SnapGuideSystem | missing | P1 | low-level | shared | required | Wave 6 | Snapping and alignment guides are explicitly needed for future features but absent from the current implementation. | Snap project nodes to grid or sibling edges.; Align prompt nodes and branch lanes visually during drag. |
| TransformHandlesOverlay | missing | P1 | high-level | shared | required | Wave 6 | Transform handles are not present today but are an expected next-step capability for richer editors and image/media nodes. | Resize or rotate future image and grouped nodes.; Scale selection frames in advanced project and prompt editors. |

## Utility and infrastructure components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CanvasSceneHost | partial | P0 | low-level | shared | required | Wave 1 | Today the host lifecycle is duplicated across CanvasWorkbench, CanvasCalendar, and legacy wrappers. A dedicated host contract reduces interop drift and gives every canvas runtime the same mounting discipline. | Mount the graph workbench inside a Blazor page and forward typed callbacks to C#.; Mount the calendar runtime inside the same shell conventions without re-implementing create/update/dispose patterns. |
| CanvasThemeTokenPack | partial | P1 | low-level | shared | none | Wave 1 | The current look is mostly encoded in a large CSS file and specialized widget styles. A formal token pack is needed for long-term consistency and skinning. | Apply consistent card, connector, overlay, and backdrop styling across Project Structure, Prompt Factory, and Calendar.; Enable a future dark mode without patching dozens of runtime-specific style fragments. |
| CommandHistoryStore | partial | P1 | low-level | shared | none | Wave 1 | Prompt Factory already has a page-local snapshot history. Project Structure and future canvases need the same capability without duplicating history stacks. | Undo selection-safe graph edits in Prompt Factory.; Undo node create/move/link actions in Project Structure after shared command integration. |
| InvalidationScheduler | missing | P0 | low-level | shared | required | Wave 1 | The current runtime has local debounce helpers but no explicit invalidation model. This is the biggest architectural gap versus a healthy long-term canvas framework. | Batch connector recalculation after multiple node moves.; Delay expensive measurements until the next animation frame. |
| JsInteropBridge | partial | P0 | low-level | shared | required | Wave 1 | Interop exists today but is monolithic and mixed with Prompt Factory helper code. A bridge layer is needed to modularize runtimes and keep C# contracts explicit. | Split generic scene host interop from Prompt Factory shortcut helpers and floating inspector code.; Keep calendar lifecycle separate from graph lifecycle while sharing host conventions. |
| LayerStack | missing | P0 | low-level | shared | required | Wave 1 | The current workbench JS has implicit layers inside one DOM/SVG runtime. Explicit layering is required for predictable z-order, event routing, and performance tuning. | Keep grid and guides below interactive content while keeping selection tools and context layers above it.; Swap connector rendering between SVG and canvas without changing page contracts. |
| SceneNodeModel | missing | P0 | low-level | shared | none | Wave 1 | The current workbench contracts describe surface DTOs but not a reusable scene graph. A formal node model is needed to stop page-level graph projection and ad hoc render bookkeeping. | Represent container nodes, connectors, overlays, and decorations in one normalized graph.; Support grouping, clipping, z-ordering, and dirty-region invalidation. |
| SerializationPersistencePack | partial | P0 | low-level | shared | partial | Wave 1 | State is persisted today, but schemas differ by page and are sometimes parsed manually. A shared persistence pack is essential for undo/redo, import/export, and migration safety. | Persist manual positions, collapse state, selection, and viewport for workbench-like surfaces.; Persist calendar view state in a typed model instead of string parsing. |
| AccessibilityMirrorLayer | missing | P2 | high-level | shared | required | Wave 6 | Canvas-heavy editors need deliberate accessibility fallback strategies; none are formalized today. | Expose selected node summaries and actionable items to assistive tech.; Mirror calendar event selection and navigation outside the visual canvas. |

## Diagnostic and developer components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| DiagnosticsOverlay | missing | P1 | high-level | shared | required | Wave 6 | The current system has no formal debug or profiling hooks, making performance and correctness issues harder to inspect. | Toggle overlays that show node bounds, connector anchors, and selection rectangles.; Display frame timing and dirty-layer counters during drag and zoom tuning. |

## Project Structure domain components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ProjectStructureActionCatalogAdapter | partial | P0 | high-level | domain-specific | none | Wave 3 | ProjectStructureCanvasCatalog is already a strong step toward reuse, but it should be formalized as a domain adapter that feeds shared create/action components. | Build contextual create menus for object types.; Build inspector create groups and action labels. |
| ProjectStructureGraphAdapter | partial | P0 | high-level | domain-specific | none | Wave 3 | Project Structure mapping exists today in the page itself. A dedicated adapter is necessary to remove page-level scene construction and make the graph reusable. | Map project objects to CanvasWorkbenchNode and CanvasWorkbenchLink.; Attach project-specific metadata, action groups, chips, and inspector payload hints. |
| ProjectStructurePlacementPolicy | partial | P1 | high-level | domain-specific | none | Wave 3 | ResolveCreatePlacement currently lives inside the page, making future placement behavior harder to reuse or validate. | Place a new child or sibling near the selected source object.; Pick sensible canvas coordinates when no source node exists. |
| ProjectStructureValidationOverlay | missing | P2 | high-level | domain-specific | partial | Wave 6 | The page already surfaces graph health information, but future richer authoring needs a first-class overlay instead of inspector-only messaging. | Show orphaned nodes, invalid dependencies, or required metadata warnings.; Annotate nodes or connectors with warning badges or helper popovers. |

## Prompt Factory domain components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PromptFactoryCatalogToolbox | partial | P0 | high-level | domain-specific | none | Wave 4 | PromptFactoryCanvasCatalog is already the seed of a domain toolbox but should be treated as a first-class adapter component. | Build session, selection, component, and input node action sets.; Drive contextual create menus and inspector create groups. |
| PromptFactorySessionGraphAdapter | partial | P0 | high-level | domain-specific | none | Wave 4 | Prompt Factory graph construction is still in the page. An adapter is required to make the session graph explicit and reusable. | Build the session graph, selection graph, branch nodes, and run-node projections.; Attach node kinds, labels, chips, and contextual actions. |
| PromptFactoryUndoRedoAdapter | partial | P1 | high-level | domain-specific | partial | Wave 4 | Undo/redo exists but is isolated inside the page. It should become a domain adapter that plugs into shared history infrastructure. | Track prompt editor state snapshots after meaningful edits.; Enable toolbar buttons and shortcuts from shared command-history state. |
| PromptRunBranchLane | partial | P1 | high-level | domain-specific | partial | Wave 4 | Prompt Factory already uses branch-specific layout ideas, but they are not formalized as a reusable lane component. | Visualize alternate prompt branches or outcomes.; Keep branch-specific nodes aligned and grouped. |
| PromptSessionAttachmentNode | partial | P1 | high-level | domain-specific | partial | Wave 4 | Attachment summaries already exist in the domain model, but canvas rendering is page-specific and lacks a reusable component definition. | Render image or file attachments on the Prompt Factory canvas.; Reuse the same attachment node pattern for future asset-heavy project canvases. |
| RecommendationOverlay | missing | P2 | high-level | domain-specific | partial | Wave 6 | PromptFactoryService already exposes recommendation-related behavior, and future roadmap items will likely require in-canvas recommendation UX. | Show suggested next blocks or missing inputs near a selected prompt node.; Present accept/reject actions for recommendations directly on the canvas. |

## Calendar domain components

| Name | Status | Priority | Level | Scope | JS bridge | Wave | Why needed | Where used |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CalendarCrudBridge | partial | P0 | low-level | shared | required | Wave 5 | CRUD interop exists but should be explicitly owned as a bridge component to avoid spreading calendar operation details across the wrapper. | Forward typed save/delete requests to ProjectWorkbenchService.; Forward playlist mutation and export requests from the widget. |
| CalendarEventEditorModal | partial | P1 | high-level | shared | required | Wave 5 | The editor exists inside the monolithic JS engine but should be treated as a named component for lifecycle, validation, and eventual reuse. | Create or edit project calendar events.; Validate required fields and contextual playlist/checklist links. |
| CalendarExportMenu | partial | P2 | high-level | shared | required | Wave 5 | Export is already part of the calendar contract, but the export interaction is still buried inside the widget runtime. | Export visible project events in different formats.; Support future share or publish flows. |
| CalendarMiniMonthNavigator | partial | P2 | high-level | shared | required | Wave 5 | This behavior exists inside the monolithic calendar runtime and should be named explicitly for customization and testing. | Jump to dates from the project calendar side panel.; Sync visible date selection with the main calendar view. |
| CalendarSelectionPanel | partial | P1 | high-level | shared | required | Wave 5 | The calendar engine already renders supporting panels, but their responsibilities are opaque and should become explicit wrapper-level concepts. | Show selected event details and connected data in the project calendar.; Surface playlist or checklist actions through typed callbacks. |
| CalendarTimeGridRenderer | partial | P1 | low-level | shared | required | Wave 5 | Timed-view rendering exists inside the monolithic calendar JS runtime but is not isolated for maintenance or targeted QA. | Render day and week time-grid views with drag/select interactions.; Support future diagnostics or customization of time-grid density and scales. |
| CanvasCalendarHost | partial | P0 | high-level | shared | required | Wave 5 | This wrapper already exists and should remain the migration target, but it needs clearer decomposition and shared host conventions. | Project calendar integration.; Potential future calendar views in other modules. |
| ProjectCalendarAdapter | missing | P0 | high-level | domain-specific | none | Wave 5 | ProjectCalendarPage still uses a legacy wrapper and string parsing. A dedicated adapter is required to finish the migration cleanly. | Map ProjectCalendarSurface and ProjectCalendarEvent to CanvasCalendarSurface and CanvasCalendarEvent.; Persist view state through ProjectWorkbenchService using typed calendar state objects. |
| ProjectCalendarStateParser | missing | P1 | low-level | domain-specific | none | Wave 5 | ProjectCalendarPage currently uses TryReadSelectedEventId over raw JSON. That is brittle and should be replaced by a typed parser/policy. | Read selected event ID, preferred view, visible date, and scope from persisted JSON.; Provide defaults when no state exists or the schema is older than current expectations. |


## Dependency highlights

The most important dependency chains are:

- CanvasSceneHost -> SceneNodeModel -> LayerStack -> InvalidationScheduler
- TextMeasureService -> TextBlockPrimitive -> NodeCardComposer
- ViewportController + HitTestService + SelectionModel + DragDropController -> advanced graph interactions
- CanvasWorkbenchShell + NodeCardComposer + ContextMenuHost + CreateActionPalette -> shared graph workbench product surface
- ProjectStructureGraphAdapter / PromptFactorySessionGraphAdapter -> shared graph shell
- CanvasCalendarHost + CalendarCrudBridge + ProjectCalendarAdapter -> project calendar migration path
- SerializationPersistencePack + CommandHistoryStore + PromptFactoryUndoRedoAdapter -> undo/redo and future clipboard readiness

## Where to find the detailed specs

Each component has its own folder under `components/`. Use `components/_INDEX.md` to navigate directly to the implementation bundle for that component.
