# QA, UX/UI, and Architecture Review

## Review posture

This review assumes the bundle should be strict enough for:

- a senior QA reviewer
- a senior UX/UI reviewer
- a senior frontend architect
- a senior canvas/framework architect

## What was checked

- Coverage of current graph and calendar surfaces relevant to the request
- Coverage of low-level primitives, interaction subsystems, and high-level shared components
- Coverage of Project Structure, Prompt Factory, and Project Calendar domain-adapter needs
- Concrete references to existing repository files instead of generic advice
- Performance, UX/UI, diagnostics, validation, and future-feature readiness
- Alignment with Konva-inspired lessons without copying Konva's exact implementation model into a C#/Blazor app where it would not fit

## Bundle completeness verdict

### 1. Are all relevant current surfaces covered?

**Yes.** The analysis and integration docs cover:

- CanvasWorkbench + CanvasWorkbenchStage
- canvasWorkbenchInterop.js + workbench CSS
- ProjectStructurePage and its service/catalog dependencies
- PromptFactoryPage and its catalog/history/service dependencies
- CanvasCalendar + calendar interop + specialized calendar runtime
- ProjectCalendarPage, ProjectEventsCalendar, and legacy workbenchInterop.js

### 2. Are low-level primitives missing from the current repo explicitly called out?

**Yes.** The bundle adds first-class coverage for:

- SceneNodeModel
- LayerStack
- InvalidationScheduler
- TextMeasureService
- ConnectorPathPrimitive
- HitTestService
- SnapGuideSystem
- TransformHandlesOverlay
- ConnectorAnchorOverlay
- ClipboardBridge
- DiagnosticsOverlay
- AccessibilityMirrorLayer

### 3. Are high-level shared components covered?

**Yes.** The bundle covers:

- CanvasWorkbenchShell
- CanvasWorkbenchStageShell
- NodeCardComposer
- GroupFrameOverlay
- CreateActionPalette
- ContextMenuHost
- TooltipPopoverHost
- InlineEditorComposer
- FloatingInspectorHost
- MinimapOverview
- CanvasCalendarHost
- CalendarSelectionPanel
- CalendarEventEditorModal

### 4. Are domain-specific needs covered?

**Yes.** The bundle contains explicit domain adapters for:

- Project Structure graph, actions, placement, and validation overlay
- Prompt Factory session graph, toolbox, branch lanes, attachment nodes, undo/redo, and recommendations
- Project Calendar adapter and typed state parser

### 5. Are performance and validation concerns included?

**Yes.** The bundle includes:

- InvalidationScheduler
- TextMeasureService caching guidance
- Viewport and minimap strategy
- DiagnosticsOverlay and performance-profiling hooks
- Component-specific performance notes and validation prompts
- Wave-based rollout to reduce migration risk

## Konva extraction review

This bundle intentionally extracted the following relevant lessons from Konva for CanDoItAll:

- Explicit scene object model and layer/group hierarchy
- Dedicated transform and drag abstractions
- Deliberate layer count and grouping discipline
- Caching and batched redraw emphasis
- Serialization of semantic app state rather than renderer internals
- Clear distinction between low-level drawing primitives and higher-level component composition

The bundle **did not** try to force a literal Konva clone into a C#/Blazor app. That would be the wrong architectural move. Instead, it translated the lessons into a Blazor-friendly shared framework structure.

## Critical review of potential weak spots

### Potential weak spot: "One framework" interpreted too literally

Risk: someone could try to force the calendar runtime into the exact same graph scene model.

Resolution in this bundle: the architecture explicitly keeps the calendar as a **sibling runtime** under the same host/state/diagnostics conventions.

### Potential weak spot: greenfield rewrite temptation

Risk: an implementation agent could build a second parallel graph wrapper instead of hardening the existing one.

Resolution in this bundle: repeated anti-pattern guidance says to extend/decompose the current `CanvasWorkbench` and retire legacy duplicates.

### Potential weak spot: page logic remains in place after adapter creation

Risk: adapters could be added without actually removing page-owned graph logic.

Resolution in this bundle: integration docs and per-component prompts explicitly call out `MapCanvasNode`, `ResolveCreatePlacement`, `BuildCanvasNodes`, `BuildCanvasLinks`, `BuildSelectionGraph`, and page-local history as extraction targets.

### Potential weak spot: advanced features added before missing primitives

Risk: future work could try to add minimap, clipboard, or recommendations without building hit testing, serialization, selection, or popover infrastructure first.

Resolution in this bundle: the wave plan and dependency map place the shared primitives ahead of advanced UX features.

## Remaining objective open points

- The graph workbench is currently a hybrid DOM/SVG runtime. The bundle recommends staying hybrid until profiling proves a stronger move toward raw canvas is necessary.
- The specialized calendar widget is still monolithic. The bundle recommends wrapper-first migration and boundary extraction rather than an immediate deep rewrite.
- No final design token palette is defined in the repository yet. The bundle introduces a theme-token component boundary but does not invent a final visual system beyond that boundary.

## QA final verdict

**Pass.** The bundle is structurally complete enough to guide systematic implementation, migration, and validation.

## UX/UI final verdict

**Pass.** The bundle addresses visual hierarchy, truncation, selection clarity, overlays, empty/loading states, wow-effect animation readiness, and future advanced editor interactions.

## Architecture final verdict

**Pass.** The bundle distinguishes foundation vs high-level components, graph vs calendar runtime families, and shared vs domain-specific responsibilities clearly enough for long-term maintainability.

## What was added specifically because of QA/future validation pressure

- ConnectorAnchorOverlay
- ClipboardBridge
- TooltipPopoverHost
- MinimapOverview
- RecommendationOverlay
- ProjectCalendarStateParser

These were all promoted to explicit components because the future-feature simulation showed they would otherwise become likely ad hoc additions.
