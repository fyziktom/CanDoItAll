# Learning Pack Canvas Engine

This document covers `C:\repositories\zyphonote-web\src\assets\js\zy-learning-pack-canvas.js`.

This file is the real reusable canvas core. It is already shared by:

- the playlist builder page in `authoring` mode
- the playlist review page in `browse` mode
- the learning study page in `study` mode

## Public entry point

The engine exports:

```js
window.ZyLearningPackCanvas = {
  create: function(options) {
    return new LearningPackCanvasController(options || {});
  }
};
```

## Supported modes

- `authoring`
- `browse`
- `study`

Mode mainly affects available interactions and context menu behavior.

## Creation options

The controller accepts these important options:

| Option | Meaning |
| --- | --- |
| `canvas` | Target `<canvas>` element |
| `host` | Bounding host that also receives overlay chrome |
| `manifest` | Hierarchical data to render |
| `packageMeta` | Root-level metadata shown on the package card |
| `scoreMap` | Score metadata lookup |
| `assetMap` | Asset lookup for image-like items |
| `defaultScoreId` | Optional default score for context actions |
| `progressMap` | Progress state keyed by item key |
| `currentItemKey` | Item to accent as current |
| `packageProgressPercent` | Root completion percentage override |
| `typeLabels` | Label overrides for package, section, score, text, image, checkpoint |
| `mode` | Interaction mode |
| `ui` | Initial zoom, pan, selection, collapse, maximize, manual positions |
| `buildContextActions` | Optional override for node context actions |
| `callbacks` | Event callbacks |

## Controller methods

The returned controller exposes these important methods:

| Method | Purpose |
| --- | --- |
| `getState()` | Return persisted UI state |
| `getSelectedNode()` | Return the primary selected node |
| `getSelection()` | Return full selection payload |
| `setData(next)` | Replace manifest, metadata, progress, labels, and optional UI state |
| `setMode(mode)` | Switch interaction mode |
| `setCurrentItem(itemKey, options)` | Change the highlighted current item |
| `setProgressState(itemKey, entry, options)` | Update one item progress record |
| `setSelection(nodeId)` | Select one node |
| `focusNode(nodeId)` | Select and center one node |
| `fitView(emitState)` | Fit canvas to scene bounds |
| `zoomBy(delta)` | Adjust zoom relative to current zoom |
| `setZoomPercent(percent)` | Set zoom in percent form |
| `setZoom(nextZoom, anchor, emitState)` | Set exact zoom around an anchor |
| `toggleHelp(forceValue)` | Open or close the help overlay |
| `toggleMaximize(forceValue)` | Toggle host maximize mode |
| `resize(forceFit)` | Recalculate canvas size and scene fit |
| `destroy()` | Remove listeners, observers, chrome, and timers |

## Callback contract

`callbacks` may include:

- `onSelectionChange(selection, meta)`
- `onStateChange(state)`
- `onAction(action)`
- `onOpenScore(scoreId, title)`

### Selection callback payload

`getSelection()` returns:

```json
{
  "primaryNode": { "...": "node payload" },
  "selectedNodes": [{ "...": "node payload" }],
  "selectedNodeIds": ["item:score_1"],
  "count": 1
}
```

### State callback payload

`getState()` returns:

- selected node ids
- collapsed node ids
- collapsed section keys
- manual positions
- current item key
- maximize state
- zoom
- panX
- panY

### Generic action payload

The engine emits action objects shaped like:

```json
{
  "type": "context-action",
  "source": "canvas",
  "mode": "authoring"
}
```

Then it extends that payload with type-specific fields, for example:

- `nodeId`
- `nodeIds`
- `node`
- `scoreId`
- `sectionKey`
- `itemKey`
- `afterItemKey`
- `afterSectionKey`
- `collapsed`
- `maximized`
- `actionId`

## Scene model

The engine builds a scene by calling `buildScene(data, uiState)`.

### Fixed hierarchy

The scene shape is fixed:

`package -> sections -> items`

This is not an arbitrary graph and not a true mindmap engine yet.

### Node kinds

The scene creates:

- one package node
- one section node per manifest section
- one item node per visible manifest item

Item nodes may represent:

- `score`
- `text`
- `checkpoint`
- `image`

### Node payload highlights

Package node payload includes:

- title and subtitle
- descriptions
- package kind and learning mode
- section, item, score, image, and required counts
- completion percent and started/completed counters
- current item key
- rect

Section node payload includes:

- `sectionKey`
- title, subtitle, summary
- item count
- estimated minutes
- completion percent
- branch status
- collapsed state
- item node ids
- rect

Item node payload includes:

- `itemKey`
- `itemType`
- title and summary
- section linkage
- score and asset metadata
- branch status
- progress status and percent
- `isCurrent`
- last measure
- rect

## View state model

The engine tracks two kinds of state:

### Content state

- manifest
- package metadata
- score map
- asset map
- progress map
- current item
- label overrides

### UI state

- selected node id
- selected node ids
- collapsed node ids
- manual positions
- zoom
- pan
- maximize flag
- help-open flag

## Viewport system

The `CanvasViewport` object provides:

- `toWorld(screenPoint)`
- `fit(bounds, width, height)`
- `clampToBounds(bounds, width, height)`
- `centerRect(rect, width, height, bounds)`
- `setZoom(nextZoom, anchor, bounds, width, height)`

This is the core pan and zoom math. It should remain in JavaScript in the Blazor version.

## Branch collapse model

Only package and section nodes can collapse.

Important details:

- package collapse hides all sections and items
- section collapse hides that section's items
- expand and collapse animate using `branchTransition`
- the engine emits `toggle-branch` actions
- collapsed children animate toward a small stacked rect inside the parent card

## Context menu model

The engine can build its own default actions or accept an override via `buildContextActions`.

That separation is exactly what the playlist builder uses:

- generic engine for rendering and hit testing
- playlist-specific actions from the page controller

## Known limitations

- fixed depth of three levels
- two-column item layout only
- no arbitrary edges between nodes
- no routing lines between sibling items
- no keyboard navigation between nodes
- no lasso selection for sections or package
- dragging changes only manual offsets, not semantic order
- no snapping, alignment guides, or minimap

## Why this is still a good Blazor foundation

- It already has a stable controller API.
- It already has state serialization.
- It already separates scene-building from host-page orchestration.
- It already supports multiple modes.
- It already proves reuse across multiple pages.

That makes it a strong candidate for a first-class JS interop component instead of a full rewrite.
