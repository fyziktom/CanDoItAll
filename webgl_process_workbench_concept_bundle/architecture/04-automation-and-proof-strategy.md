# Automation and proof strategy

## Required runtime namespace

The new runtime should expose a namespace similar to the current canvas runtime:

- `window.CanDoItAll.webglWorkbench`

## Required semantic helpers

- `create(host, ...)`
- `update(host, ...)`
- `fitView(host)`
- `focusNode(host, nodeId)`
- `getState(host)`
- `getSceneSnapshot(host)`
- `getDiagnostics(host)`
- `exportImageData(host)`
- `simulateDrag(host, request)`
- `simulateConnection(host, request)`
- `finishInteraction(host)`
- `getAnchorCenter(host, request)` or equivalent

## Required host debug state

- `host.__webglWorkbenchState`

Suggested contents:

- current surface/scene input,
- lookup maps by node/edge/port ID,
- projected anchor bounds,
- camera state,
- selection state,
- deterministic-mode flag,
- diagnostics metrics.

## Screenshot rule

Every browser-proof run should capture both:

- normal browser screenshots,
- runtime-exported image data where supported.

That gives the concept two different proof paths for the same scenario.

## Deterministic mode rule

The sandbox and runtime must support a deterministic mode that disables easing or timing-dependent animation so proof remains repeatable.
