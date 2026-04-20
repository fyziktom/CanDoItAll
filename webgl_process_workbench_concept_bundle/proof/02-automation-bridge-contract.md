# Automation bridge contract

## Required global namespace

- `window.CanDoItAll.webglWorkbench`

## Required methods

| Method | Minimum purpose |
| --- | --- |
| `create(host, ...)` | Create and initialize the runtime for a host element. |
| `update(host, ...)` | Apply a new scene/surface payload. |
| `fitView(host)` | Frame the current scene deterministically. |
| `focusNode(host, nodeId)` | Move camera to a selected node or preset view. |
| `getState(host)` | Return coarse UI/runtime state. |
| `getSceneSnapshot(host)` | Return serializable node/edge/camera/anchor data. |
| `getDiagnostics(host)` | Return render/debug metrics. |
| `exportImageData(host)` | Return an image-data payload or equivalent export. |
| `simulateDrag(host, request)` | Semantically move a node. |
| `simulateConnection(host, request)` | Create/remove/reroute a connection semantically. |
| `finishInteraction(host)` | Explicitly release any active interaction. |
| `getAnchorCenter(host, request)` | Return screen-space center for node/port/edge handles. |

## Required host debug state

- `host.__webglWorkbenchState`

Minimum contents:

- scene input,
- lookup maps,
- projected bounds,
- selected IDs,
- camera state,
- deterministic-mode flag,
- diagnostics counters.

## Required DOM mirror attributes

- `data-webgl-node-id`
- `data-webgl-port-id`
- `data-webgl-edge-id`
- `data-webgl-anchor-role`
- `aria-label` values that describe the semantic element

## Determinism rule

All proof scenarios must be runnable with a deterministic mode that freezes or removes camera easing and non-essential animation jitter.
