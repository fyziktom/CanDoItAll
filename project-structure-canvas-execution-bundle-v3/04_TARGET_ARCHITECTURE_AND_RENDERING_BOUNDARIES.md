# Target architecture and rendering boundaries

## Design goal

The target is a **real canvas workbench runtime** with the correct division of responsibility:

- dense scene rendering is canvas-owned,
- domain and persistence stay in typed C#,
- rich UI surfaces remain HTML/Blazor,
- hot-path interaction state stays local in JS until commit.

## Target stage composition

The runtime workbench stage should evolve toward this shape:

```html
<div class="cw-stage-surface">
  <div class="cw-canvas-stack">
    <canvas class="cw-canvas-backdrop"></canvas>
    <canvas class="cw-canvas-scene"></canvas>
    <canvas class="cw-canvas-overlay"></canvas>
    <div class="cw-html-overlay-root"></div>
    <div class="cw-a11y-mirror-root"></div>
  </div>
</div>
```

### Layer intent

#### `cw-canvas-backdrop`
Own:
- grid/background,
- static scene backdrop,
- low-cost decorations that do not need hit regions.

#### `cw-canvas-scene`
Own:
- node cards,
- links,
- group frames,
- minimap visuals,
- dense scene annotations.

#### `cw-canvas-overlay`
Own:
- marquee rectangle,
- snap guides,
- hover rings,
- drag previews,
- other transient interaction overlays.

#### `cw-html-overlay-root`
Own:
- context menus,
- tooltips/popovers,
- quick create composer,
- inline editor overlays,
- dialogs that are anchored to projected canvas coordinates,
- any active-node HTML escape hatch required for parity.

#### `cw-a11y-mirror-root`
Own:
- accessibility mirror and live announcements.

## Ownership split

## JavaScript hot path ownership

### Renderer and scheduling
JS should own:
- canvas context creation,
- device-pixel-ratio scaling,
- requestAnimationFrame scheduling,
- dirty redraw decisions,
- viewport culling,
- scene projection caches,
- hit-test structures,
- renderer metrics.

### Interaction
JS should own:
- pan/zoom,
- drag/marquee,
- pointer capture,
- hot-zone hit testing,
- minimap navigation,
- transient hover state,
- overlay-vs-scene routing.

### Transient interaction state
Keep local in JS until commit:
- drag delta,
- current viewport while moving,
- hover target,
- current hot-zones,
- in-progress manual positions,
- dirty rectangles,
- local canvas metrics.

## C# ownership

C# should continue to own:
- `CanvasWorkbenchSurface` and typed node/link/window contracts,
- graph adapters,
- domain semantics,
- create/edit/delete/link operations,
- service calls and persistence,
- product-specific action catalogs,
- committed view state,
- restore semantics for selection/windows.

## HTML/Blazor ownership

These should remain HTML:
- toolbox floating window,
- selection and health windows,
- help/settings overlays,
- summary modal,
- transcript/provider confirmation,
- mermaid viewer,
- preview dialogs,
- upload/file inputs,
- advanced editors and forms.

Moving these to canvas would increase product risk and not attack the dense-scene bottleneck.

## Hit testing in the new renderer

The new renderer must stop depending on DOM node elements for runtime hit testing.

Instead, maintain a geometry-driven hit model:
- node bounds,
- collapse button hotspot,
- compact-path copy hotspot,
- optional media or status hotspots if needed,
- frame handles,
- minimap bounds.

The recommended shape is a JS-owned list or spatial index of hit regions for the current frame.

## Node renderer parity strategy

A full node-to-canvas move is desirable, but the safest way to keep parity is:

1. draw the visual node card in canvas,
2. keep node interactions through geometry hot-zones,
3. keep only difficult active UI in HTML overlays.

That means:
- no DOM element per node in normal runtime mode,
- optional HTML overlay for the currently edited note or composer,
- optional HTML overlay for any parity-critical active-node control.

## State model

### A) Domain state
Server-owned, typed, durable:
- nodes,
- links,
- persisted positions,
- business metadata.

### B) Interaction state
Client-only until commit:
- drag positions,
- viewport while moving,
- hover,
- local canvas caches,
- temporary hot-zone data.

### C) Committed view snapshot
Persist only when:
- interaction ends,
- explicit idle checkpoint fires,
- a user action commits state intentionally.

## Export model target

Once the runtime scene is canvas-owned, export should be renderer-owned too:
- compose scene canvases directly into export canvas,
- optionally render HTML-only overlay exceptions only when needed,
- avoid DOM clone / SVG foreignObject dependence for the main scene.

## Rollout rule

Use a safe staged rollout:
- keep `CanvasWorkbench` public API stable,
- allow a runtime feature flag or internal renderer mode while parity is being proven,
- remove the fallback only after all required browser and export gates are green.
