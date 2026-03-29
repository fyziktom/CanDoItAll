# True-canvas migration plan

## Principle

Do not do a reckless big-bang rewrite.  
Migrate the runtime scene in layers while preserving the public `CanvasWorkbench` API and validating after each stage.

## Migration stages

## Stage 1 — Real canvas stage shell
Introduce a real canvas stack in the runtime workbench host:
- backdrop canvas,
- scene canvas,
- overlay canvas,
- HTML overlay root,
- accessibility mirror.

At this stage, the old renderer can still exist behind a fallback mode if needed.

## Stage 2 — Links to canvas
Move links first because:
- they contribute many DOM/SVG elements,
- they do not require rich HTML controls,
- they are a high-density scene element with clear performance upside.

## Stage 3 — Minimap, diagnostics marks, group frames to canvas
These are also high-leverage dense-scene visuals with low product risk compared with full node migration.

## Stage 4 — Nodes to canvas
Move the visible runtime node cards to canvas, but preserve behavior through:
- node bounding-box hit regions,
- sub-hit regions for collapse and compact-path copy,
- HTML overlay escape hatches for context menu, inline editors, and parity-critical active controls.

## Stage 5 — Export rewrite
Once the scene is truly canvas-owned, export should be canvas-owned too.

## Stage 6 — Remove legacy runtime scene path
Only after full parity and regression proof.

## Hot-zone model for canvas nodes

Each visible node should expose geometry metadata such as:
- `nodeBodyRect`
- `collapseHotZone`
- `compactPathHotZone`
- `mediaHotZone` (if needed later)
- `frameHandleHotZones` (when relevant)

The interaction router should first test these hot-zones, then fall back to node-body selection/open behavior.

## LOD rule for dense scenes

A canvas renderer should not always paint the full card at every zoom level.

Recommended policy:
- above a detail threshold: draw full card with text/chips,
- in a medium range: draw reduced text and core status markers,
- in a low-detail range: draw simple blocks with palette and title stub only.

This keeps dense scenes readable without paying full text layout cost at every zoom.

## Text measurement strategy

Use a JS-owned text measurement cache:
- cache by font + string + max width,
- reuse measured lines across redraws,
- invalidate only when theme or zoom bucket changes.

## Dirty redraw strategy

Do not redraw the whole scene on every pointer move when it is avoidable.

Track dirty reasons such as:
- viewport transform changed,
- dragged node positions changed,
- selection/hover changed,
- diagnostics visibility changed.

If the implementation becomes too complex for partial redraw in early stages, prefer:
- requestAnimationFrame batching,
- culling,
- simplified LOD,
before introducing overly clever region slicing.

## Renderer rollout rule

Use one of these rollout patterns:
- internal renderer mode switch,
- surface/chrome capability flag,
- safe fallback path behind configuration.

This protects PromptFactory and lets Codex prove parity before removing the old path.

## Do not move these into the scene renderer

Do not paint these into the main scene canvas:
- toolbox,
- selection window,
- health window,
- summary modal,
- transcript dialog,
- mermaid dialog,
- file upload/editor forms.

These must remain HTML overlays or dialogs.

## Exit rule for node migration

Node migration is only considered done when:
- runtime scene DOM count drops materially,
- node selection/drag/open parity survives,
- collapse still works,
- compact-path copy still works,
- context menu still works,
- inline note edit flow still works,
- attachment preview and export still work.
