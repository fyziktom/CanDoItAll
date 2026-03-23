# Konva Reference Extraction

## Why Konva matters here

Konva is not a ready-made final UI component library. Its real value for CanDoItAll is architectural:

- object model over raw canvas
- explicit stage/layer/group/shape separation
- transform, drag, selection, and hit-testing patterns
- redraw and caching discipline
- custom-shape extensibility without breaking the event/render core

## Local clone path for Codex

The user stated that Codex will have Konva cloned at:

```text
c:\repositories\konva
```

## Recommended Konva files to inspect

- c:\repositories\konva\src\Animation.ts
- c:\repositories\konva\src\Container.ts
- c:\repositories\konva\src\Context.ts
- c:\repositories\konva\src\Factory.ts
- c:\repositories\konva\src\Group.ts
- c:\repositories\konva\src\Layer.ts
- c:\repositories\konva\src\Node.ts
- c:\repositories\konva\src\Shape.ts
- c:\repositories\konva\src\Stage.ts
- c:\repositories\konva\src\Util.ts
- c:\repositories\konva\src\shapes\Image.ts
- c:\repositories\konva\src\shapes\Line.ts
- c:\repositories\konva\src\shapes\Transformer.ts

## Extracted lessons and direct application to CanDoItAll


### 1. Use an explicit object model over raw canvas APIs

**Evidence summary:** Konva positions itself as an object model over the native 2D canvas context and organizes content into stage/layer/group/shape abstractions.

**Konva files to inspect locally**

- c:\repositories\konva\src\Stage.ts
- c:\repositories\konva\src\Layer.ts
- c:\repositories\konva\src\Group.ts
- c:\repositories\konva\src\Node.ts
- c:\repositories\konva\src\Shape.ts
- c:\repositories\konva\src\Container.ts

**Application to CanDoItAll**

Introduce SceneNodeModel, LayerStack, and clear graph-vs-calendar runtime boundaries rather than continuing with one monolithic JS file plus page mapping logic.

### 2. Keep layer count deliberate and use grouping to control complexity

**Evidence summary:** Konva warns that too many layers hurt performance and suggests using groups to rethink structure.

**Konva files to inspect locally**

- c:\repositories\konva\src\Stage.ts
- c:\repositories\konva\src\Group.ts

**Application to CanDoItAll**

Keep real render layers limited: backdrop, connectors, nodes, overlays, diagnostics. Use group frames/lanes/scene groups inside those layers rather than many top-level layers.

### 3. Treat drag, transform, and selection as first-class subsystems

**Evidence summary:** Konva has dedicated draggable behavior and a Transformer abstraction for resize/rotate interactions.

**Konva files to inspect locally**

- c:\repositories\konva\src\Node.ts
- c:\repositories\konva\src\shapes\Transformer.ts

**Application to CanDoItAll**

Add DragDropController, TransformHandlesOverlay, SelectionModel, and ConnectorAnchorOverlay instead of scattering drag/selection logic across page code and generic runtime helpers.

### 4. Design around caching and batched redraws

**Evidence summary:** Konva emphasizes batch draws, caching, and selective listening/hit detection for performance.

**Konva files to inspect locally**

- c:\repositories\konva\src\Layer.ts
- c:\repositories\konva\src\Animation.ts
- c:\repositories\konva\src\Util.ts
- c:\repositories\konva\src\Context.ts

**Application to CanDoItAll**

Create InvalidationScheduler, cache text/layout/connector geometry, and avoid full rebuilds during drag, zoom, or hover loops.

### 5. Serialization should prioritize app state, not raw renderer internals

**Evidence summary:** Konva supports JSON serialization but also recommends saving application state for complex apps.

**Konva files to inspect locally**

- c:\repositories\konva\src\Node.ts
- c:\repositories\konva\src\Container.ts
- c:\repositories\konva\src\Factory.ts

**Application to CanDoItAll**

Use SerializationPersistencePack and domain adapters to persist semantic scene state rather than raw DOM or renderer implementation details.

### 6. Support custom shapes and context-level extensions without sacrificing a stable core

**Evidence summary:** Konva's Context and Shape layers support custom drawing while preserving a common scene and event model.

**Konva files to inspect locally**

- c:\repositories\konva\src\Context.ts
- c:\repositories\konva\src\Shape.ts
- c:\repositories\konva\src\shapes\Line.ts
- c:\repositories\konva\src\shapes\Image.ts

**Application to CanDoItAll**

Separate low-level primitives (text, image, connector, container) from high-level domain cards and overlays so custom components can grow on a stable base.


## What not to copy blindly from Konva

- Do not import Konva as the main production runtime for the existing app. The goal is architectural inspiration, not dependency replacement.
- Do not mirror Konva's API one-to-one in C#. Adapt the principles to Blazor, typed DTOs, and the current product constraints.
- Do not force the specialized calendar runtime into the graph scene model just because Konva has one scene graph.

## Konva-inspired checklist for implementation agents

- Does the shared graph subsystem now have an explicit node/layer/group model?
- Is redraw invalidation explicit and batched?
- Are drag and transform subsystems first-class rather than scattered?
- Are hit-testing boundaries explicit and testable?
- Is semantic app state persisted instead of renderer internals?
- Can new low-level primitives be introduced without editing giant monolithic runtime code paths?
