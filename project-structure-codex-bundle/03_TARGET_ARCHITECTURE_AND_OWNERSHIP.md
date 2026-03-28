# Target architecture and ownership

## Design goal

Make the workbench performant **without** throwing away the strengths of Blazor and typed C# models.

The right target is a hybrid system where:

- the **hot render/interaction path** lives in well-structured plain JavaScript,
- **domain and product logic** stay in C#,
- rich overlays remain HTML/Blazor,
- persistence happens on **commit/idle**, not during every pointer move.

## Ownership matrix

## JavaScript owns the hot path

### Scene rendering
- retained node/link/frame element maps,
- incremental patching,
- viewport culling,
- transform-only pan,
- drag loop and dirty-scene patching,
- snap guides and transient drag overlays,
- hover and interaction hit testing.

### Input ownership
- scene pointer capture,
- wheel routing,
- overlay-vs-scene input ownership,
- frame drag and marquee drag,
- local focus handoff where required.

### Transient UI state
- drag-in-progress coordinates,
- viewport interpolation/animation,
- dirty region tracking,
- local layout cache,
- floating-window geometry while moving/resizing,
- instrumentation counters.

## C# owns durable product logic

### Domain and persistence
- `ProjectStructureNode` and related typed models,
- service methods and DB transactions,
- create/edit/delete/link/hierarchy mutations,
- system-managed graph synchronization,
- typed artifact actions and command routing,
- view-state persistence once the interaction is committed.

### Product projection
- action catalogs,
- graph adapters,
- typed label/palette/annotation construction,
- export orchestration,
- selection-window product semantics,
- runtime launch integration.

## HTML/Blazor owns rich overlays

These should remain HTML first:
- toolbox,
- selection window,
- health window,
- quick action dialog,
- hierarchy dialogs,
- summary modal,
- transcript confirmation,
- mermaid viewer,
- attachment previews,
- upload and form-heavy composer flows.

Why:
- accessibility,
- browser-native form behavior,
- simpler focus handling,
- lower implementation risk.

## State model target

### A. Domain graph state
Persisted on the server.  
Examples:
- nodes and links,
- node metadata,
- route/action payloads,
- persisted X/Y positions for ProjectStructure nodes.

### B. Live interaction state
Client-only in JS until commit.  
Examples:
- pan and zoom while moving,
- drag delta,
- hover,
- in-progress manual positions,
- live snap guide state,
- floating-window in-flight geometry.

### C. Persisted view snapshot
Saved only when the interaction ends or after an intentional idle checkpoint.  
Examples:
- final zoom,
- final pan,
- selected IDs if product wants restore,
- collapsed IDs,
- final window geometry,
- group frames.

### D. Overlay UI state
Mirrored in Blazor only when needed by visible HTML overlays.  
Examples:
- selected IDs for selection window,
- currently opened dialog,
- currently visible windows,
- current search text.

## Important ProjectStructure-specific rule

For ProjectStructure, persisted node positions should be owned by **domain node X/Y**.

`CanvasWorkbenchUiState.ManualPositions` can still exist as a generic workbench concept, but for ProjectStructure it should be treated as:
- transient drag state,
- or a local-only fallback,
- not a second long-lived persisted position source.

## JS structure recommendation (plain JS only)

Do not introduce TypeScript.

Use plain JavaScript and keep the public API stable, for example:

- `window.CanDoItAll.canvasWorkbench.create(...)`
- `window.CanDoItAll.canvasWorkbench.update(...)`
- `window.CanDoItAll.canvasWorkbench.dispose(...)`

Internally, organize responsibilities as explicit JS modules or internal objects:
- `stateStore`
- `overlayGuards`
- `viewportController`
- `retainedRenderer`
- `scenePatching`
- `selectionModel`
- `floatingWindowBridge`
- `instrumentation`

This can be done either:
- inside one file first for safety,
- or split into multiple plain JS files later, without a new build chain.

## Migration strategy

## Phase 1: stop the bleeding
- overlay guards,
- commit-only persistence,
- batched move persistence,
- no full reloads for simple mutations,
- instrumentation.

## Phase 2: make current renderer fast
- retained DOM/SVG patching,
- viewport culling,
- dirty-region drag updates,
- overlay decomposition.

## Phase 3: clean architecture
- JS module separation,
- dedicated browser regression suite,
- optional consolidation of duplicate shared libraries.

## Phase 4: only if needed
- benchmarked true-canvas renderer spike.

## Why not jump directly to true canvas

A direct renderer rewrite would force you to solve at once:
- hit testing,
- text measurement,
- accessibility mirroring,
- export compatibility,
- overlay integration,
- selection/highlight semantics,
- regression risk across PromptFactory and Sandbox.

That is too much risk before the obvious bottlenecks are fixed.

## Final rule

Push **more responsibility to JavaScript**, but only where JavaScript is the right owner:
- interaction mechanics,
- rendering mechanics,
- hot-path local state.

Keep **domain truth in C#** and keep **rich overlay UI in HTML/Blazor**.
