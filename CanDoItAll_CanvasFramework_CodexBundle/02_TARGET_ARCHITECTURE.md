# Target Architecture

## Strategic frame

The target architecture should produce **one coherent canvas framework family** for CanDoItAll, not a bag of isolated one-off widgets.

That family should contain:

- a **shared host and foundation**
- a **shared graph-workbench subsystem**
- a **shared calendar-wrapper subsystem**
- **domain adapters** for Project Structure, Prompt Factory, and Project Calendar

This deliberately avoids a false simplification: the calendar runtime is **not** the same thing as the graph editor runtime. They should share contracts, lifecycle, persistence, diagnostics, theming, and integration patterns — not be forced into the same rendering engine.

## Architectural principles

- Keep domain logic, persistence, action resolution, and validation orchestration in C# whenever practical.
- Use JS for low-level rendering, pointer math, DOM measurement, host integration, and browser-native APIs.
- Prefer decomposition of the current shared workbench over replacement by a new parallel framework.
- Separate low-level primitives from high-level UI components so future domain features can be assembled rather than reimplemented.
- Make every hot-path rendering or interaction subsystem explicit and testable.
- Use typed persistence models and versioned UI state rather than page-local JSON probing.
- Design for hybrid rendering: DOM where rich cards/editors are needed, SVG/canvas where geometry-heavy overlays or connectors benefit from it.

## Proposed layered model


## Canvas host and render foundation

**Responsibility:** Lifecycle, host DOM, resize observation, theme propagation, diagnostics toggles, and runtime mounting.

**Must stay shared**

- CanvasSceneHost
- JsInteropBridge
- CanvasThemeTokenPack

**May remain domain/canvas specific**

- Runtime-specific renderer implementation for graph or calendar

## Scene graph and render primitives

**Responsibility:** Normalized scene nodes, layer stack, invalidation model, text/image/icon/container/connector primitives, and serializable state boundaries.

**Must stay shared**

- SceneNodeModel
- LayerStack
- InvalidationScheduler
- TextMeasureService
- TextBlockPrimitive
- ImagePrimitive
- ConnectorPathPrimitive

**May remain domain/canvas specific**

- Domain adapters define which primitives to compose for each node type

## Layout, transform, and viewport

**Responsibility:** Placement, measurement feedback, grouping bounds, zoom/pan math, coordinate transforms, and future virtualized layout support.

**Must stay shared**

- LayoutEngine
- ViewportController
- GridBackdrop
- MinimapOverview

**May remain domain/canvas specific**

- ProjectStructurePlacementPolicy
- PromptRunBranchLane

## Interaction and selection

**Responsibility:** Hit testing, selection, hover/focus, drag/drop, snapping, marquee, transform handles, connector anchors, keyboard shortcuts, and clipboard.

**Must stay shared**

- HitTestService
- SelectionModel
- HoverFocusRouter
- DragDropController
- SnapGuideSystem
- MarqueeSelectionOverlay
- TransformHandlesOverlay
- ConnectorAnchorOverlay
- KeyboardShortcutRouter
- ClipboardBridge

**May remain domain/canvas specific**

- Domain-specific action semantics executed by adapters or services

## High-level graph workbench components

**Responsibility:** Shell chrome, stage layout, node-card composition, context actions, create palette, inline editors, group frames, floating inspector, and state overlays.

**Must stay shared**

- CanvasWorkbenchShell
- CanvasWorkbenchStageShell
- NodeCardComposer
- GroupFrameOverlay
- CreateActionPalette
- ContextMenuHost
- TooltipPopoverHost
- InlineEditorComposer
- FloatingInspectorHost
- EmptyStateOverlay
- SkeletonStateOverlay

**May remain domain/canvas specific**

- ProjectStructureGraphAdapter
- PromptFactorySessionGraphAdapter

## Calendar runtime family

**Responsibility:** Typed wrapper plus specialized calendar renderers, selection panels, editor modal, CRUD bridge, export, and domain adapters.

**Must stay shared**

- CanvasCalendarHost
- CalendarTimeGridRenderer
- CalendarMiniMonthNavigator
- CalendarSelectionPanel
- CalendarEventEditorModal
- CalendarCrudBridge
- CalendarExportMenu

**May remain domain/canvas specific**

- ProjectCalendarAdapter
- ProjectCalendarStateParser

## State, persistence, animation, diagnostics, and accessibility

**Responsibility:** Undo/redo, serialized UI state, animation scheduling, diagnostics, test hooks, and semantic fallback layers.

**Must stay shared**

- SerializationPersistencePack
- CommandHistoryStore
- AnimationTimeline
- DiagnosticsOverlay
- AccessibilityMirrorLayer

**May remain domain/canvas specific**

- PromptFactoryUndoRedoAdapter
- ProjectStructureValidationOverlay
- RecommendationOverlay


## Communication model between layers

### C# owns

- Domain models and service calls
- Domain-to-shared-surface projection
- Action catalogs and command orchestration
- Typed persistence and UI state envelopes
- Feature flags, permissions, and validation policy
- Most tests that should remain fast, deterministic, and business-aware

### JS owns

- Host runtime lifecycle inside the mounted canvas surface
- Hot-path pointer loops, drag, hover, and wheel/touch event processing
- Low-level hit testing and geometry calculations
- Text measurement, browser layout measurement, and fine-grained overlay positioning
- Actual rendering implementation details of the graph runtime and the specialized calendar runtime

### Shared contract between C# and JS

- Typed surface payloads
- Typed state change payloads
- Typed selection payloads
- Typed operation requests (save/delete/export/create/etc.)
- Diagnostics and test-hook toggles

## Recommended project/package organization

```text
src/
  CanDoItAll.ComponentKit/
    Canvas/
      Core/
      Graph/
      Calendar/
      Diagnostics/
    Components/
    wwwroot/js/
      canvas-scene-host.js
      canvas-graph-runtime.js
      canvas-layout.js
      canvas-interaction.js
      canvas-overlays.js
      canvas-context-menu.js
      canvas-inline-editor.js
      canvas-calendar-host.js
      canvas-calendar-crud.js
      floating-inspector.js
      prompt-factory-shortcuts.js
  CanDoItAll.Modules.Workbench/
    CanvasAdapters/
    Calendar/
  CanDoItAll.Modules.Factory/
    CanvasAdapters/
tests/
  CanDoItAll.Tests.Components/
```

The exact folder names can vary, but the ownership boundaries should not.

## Scene graph or equivalent

### Recommendation

Introduce an explicit internal scene model for the **graph-workbench family**:

- node/container hierarchy
- named layers
- bounds and transforms
- visibility and hit flags
- caching and invalidation hints
- semantic metadata for accessibility and diagnostics

This scene model should **not** be exposed directly as the page API. Pages should continue to work through shared typed surface DTOs and domain adapters.

### Why

Without a first-class scene model:

- layout logic remains hidden inside one JS file
- overlays have no stable anchor semantics
- hit testing stays ad hoc
- future minimap, transform handles, and connector routing become fragile

## Layering

Use an explicit limited layer stack:

1. Backdrop/grid
2. Connector geometry
3. Rich node/content layer
4. Overlay tools (selection, marquee, handles, anchors, popovers)
5. Diagnostics + accessibility mirror support

Do **not** create many top-level render layers. Keep the layer count small and use grouping inside layers.

## Grouping

Grouping should exist at two levels:

- **Structural grouping** in the scene model: parent/child containers, branch lanes, grouped node clusters
- **Visual grouping** as overlays or frames: selection borders, milestone frames, validation regions

Group semantics must be serializable and must not depend on DOM query structure.

## Hit testing

Create a shared `HitTestService` that returns semantic targets, not raw DOM nodes.

Target types should include at minimum:

- node card
- connector path
- connector anchor
- group frame
- transform handle
- overlay action
- empty stage/backdrop

This service can still use DOM or SVG internals under the hood in the current hybrid renderer.

## Redraw invalidation

Introduce `InvalidationScheduler` with dirty flags such as:

- scene structure dirty
- node bounds dirty
- connector geometry dirty
- viewport dirty
- selection overlay dirty
- diagnostics dirty

The scheduler should batch work on `requestAnimationFrame` and avoid whole-surface rebuilds during active interactions.

## Caching

Use targeted caching for:

- text measurement
- measured card sizes
- connector path geometry
- simplified minimap projection
- validation overlay markers

Avoid generic premature caching of whole rendered DOM trees unless profiling shows it is needed.

## Virtualization and large-scene fallback

The graph workbench should support a pragmatic large-scene strategy:

- Only fully realize rich card DOM for nodes inside or near the viewport.
- Use simplified placeholders for far-off-screen nodes when scene density grows.
- Keep connector calculations limited to visible or near-visible entities where possible.
- Provide minimap and outline navigation as a companion to virtualization.
- Expose performance counters through DiagnosticsOverlay before scene complexity grows too far.

The calendar runtime should instead use **view-specific** performance strategies such as time-windowing or density-aware event layout.

## Zoom and pan

The shared viewport controller must support:

- wheel zoom anchored to cursor
- explicit zoom buttons
- fit-to-view
- focus specific node
- maximize/unmaximize stability
- coordinate transforms between host and scene
- future touch pinch support

Viewport state should be serializable and versioned.

## Snapping and alignment guides

Snapping should be a dedicated subsystem, not a side effect of layout math.

Recommended snapping modes:

- grid snap
- center-to-center alignment
- edge-to-edge alignment
- lane alignment
- optional connector-anchor snap

Guides should be rendered in a transient overlay layer and respect zoom level.

## Selection box

Marquee selection is already present. The target improvement is to formalize it as a reusable overlay with policy options:

- replace selection
- additive selection
- intersection vs containment selection
- type filters

## Transform handles

Transform handles should be a standalone overlay subsystem with read-only awareness and future support for:

- resize
- rotate
- aspect-ratio lock
- keyboard nudging
- snap-aware transforms

## Drag and drop

The drag controller should own:

- pointer capture
- drag threshold
- multi-node move
- group frame move
- future external drop targets
- future branch reorder
- future template insertion

Persistence of the final result remains a C# responsibility.

## Keyboard interaction

Keyboard handling should be centralized through `KeyboardShortcutRouter`:

- zoom shortcuts
- fit view
- help overlay
- undo/redo
- delete
- clipboard
- navigate selection
- future command palette

Scope awareness is essential so text inputs and modals are not broken.

## Context menu integration

Keep context menus as a shared overlay subsystem with:

- nested menu support
- viewport-aware placement
- keyboard navigation
- focus return
- action grouping
- disabled-state reasoning

Context menus should consume shared action metadata from domain adapters.

## Clipboard scenarios

Clipboard should support at least:

- duplicate selection
- copy/paste internal graph payloads
- versioned payload schema
- undo/redo integration
- future cross-surface compatibility policy

Do not implement clipboard as raw DOM cloning.

## Accessibility fallbacks

Canvas-heavy UIs still need accessibility fallback strategies. The framework should include:

- semantic mirror layer
- keyboard-reachable controls for canvas actions
- tooltips or inspector summaries for truncated text
- non-color-only validation states
- reduced-motion compliance for animated interactions

## Export/import and serializable state

The framework should be ready for:

- serializable viewport state
- serializable selection state where appropriate
- semantic scene export/import envelopes
- domain-specific export layers built on shared serialization

Persist semantic app state, not raw renderer internals.

## Undo/redo readiness

Undo/redo should be treated as a cross-cutting capability supported by:

- shared serialization envelopes
- `CommandHistoryStore`
- domain adapters that can capture and restore meaningful snapshots
- keyboard shortcut routing
- toolbar enablement from one source of truth

## Plugin/extensibility model

Future extensibility should follow a controlled plugin model:

- domain adapter registers node kinds, action catalogs, and optional templates
- shared framework registers primitives, overlays, and diagnostics hooks
- optional extension components can plug into:
  - node-card templates
  - toolbar/action sections
  - validation overlays
  - export providers
  - diagnostics panels

Avoid arbitrary runtime mutation of the scene model from untyped plugin code.

## Performance strategy

The target performance strategy is:

- Keep hot interaction math in JS.
- Keep rich semantics and persistence orchestration in C#.
- Use explicit invalidation and measurement caches.
- Virtualize rich DOM card realization when scenes grow.
- Limit layer count and keep grouping explicit.
- Profile before moving more of the rich node card rendering to raw canvas.

## Testability and validation strategy

To keep the framework testable:

- Pure C# mapping/layout/persistence policies should have deterministic unit tests.
- Blazor wrappers and shells should have component tests.
- JS-heavy subsystems should expose diagnostics/test hooks and stable payload contracts.
- Every migration wave should include at least one regression test against the affected page.
- Future feature simulation should remain part of the release gate for the framework.

## Technical-debt containment rules

- Every page-level canvas rule extracted into a domain adapter reduces long-term debt.
- Every shared runtime file split by concern reduces accidental coupling.
- Every typed persistence model replaces brittle raw-JSON page parsing.
- Every new advanced interaction should land on top of explicit low-level primitives, not page-local hacks.

## Final target-architecture verdict

The healthiest target for CanDoItAll is a **hybrid shared canvas framework**:

- **one shared host/foundation**
- **one graph-workbench subsystem** derived from the current `CanvasWorkbench`
- **one specialized calendar subsystem** wrapped by `CanvasCalendar`
- **domain adapters** that keep Project Structure, Prompt Factory, and Project Calendar expressive without duplicating infrastructure
