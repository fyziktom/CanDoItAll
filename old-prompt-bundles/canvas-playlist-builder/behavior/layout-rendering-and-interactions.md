# Layout, Rendering, And Interactions

This document covers the visual structure, geometry, and input behavior that should be preserved in the Blazor port.

## Page layout around the canvas

The playlist builder page is not canvas-only. It is a larger workspace:

- main stage card with canvas header and canvas host
- right-side inspector
- lower outline and manifest editor section
- lower score-library section
- separate overview tab with basics form

The canvas therefore works as one editor surface inside a larger authoring page, not as a full-screen standalone app by default.

## Host CSS model

Important classes in `input.css`:

- `.lp-stage-canvas`
- `.lp-stage-inspector`
- `.lp-canvas-host`
- `.lp-canvas-host.is-maximized`
- `.lp-learning-canvas`
- `.lp-canvas-ui`
- `.lp-canvas-toolbar`
- `.lp-canvas-zoom-panel`
- `.lp-canvas-hint`
- `.lp-canvas-help-overlay`

Important behavior:

- the host has rounded corners and overflow hidden
- the host has a tall responsive min-height
- maximize mode turns the host into a fixed overlay
- `body.lp-canvas-maximized` disables page scrolling
- the canvas fills the host
- overlay controls are positioned above the canvas using absolutely positioned UI chrome

## Core geometry constants

The engine uses fixed geometry constants:

| Constant | Value |
| --- | --- |
| `PACKAGE_WIDTH` | `360` |
| `PACKAGE_HEIGHT` | `188` |
| `SECTION_WIDTH` | `304` |
| `SECTION_HEIGHT` | `164` |
| `ITEM_WIDTH` | `278` |
| `ITEM_HEIGHT` | `152` |
| `ITEM_GAP` | `24` |
| `SECTION_GAP` | `48` |
| `SECTION_TO_ITEMS_GAP` | `78` |

These values define the visual rhythm. If the Blazor port changes them too early, the port will no longer match the proven layout.

## Scene layout rules

### Package placement

- one package card sits at the top-left world origin area
- it acts as the root
- it summarizes the whole structure

### Section placement

- sections are placed vertically under the package
- each section gets one group row
- section height stays fixed
- group height expands when a section has many visible items

### Item placement

- visible items of a section are placed to the right of the section
- items use a fixed two-column grid
- row count depends on visible item count
- collapsing a section hides the items from layout

### Manual offsets

- any node can receive a manual `x/y` offset from `manualPositions`
- dragging changes offset, not underlying semantic order
- section dragging also drags child item nodes

## Rendering model

### Background

The engine paints:

- a warm-to-cool diagonal gradient
- a light dot pattern

This background is part of the editor feel. It is not decorative noise only. It provides depth and movement without fighting the nodes.

### Connectors

- connectors are drawn first
- they are bezier curves
- they connect package to sections and sections to items
- selected nodes are drawn later so they sit visually on top

### Node drawing

There are specialized renderers for:

- `drawPackageNode`
- `drawSectionNode`
- `drawItemNode`

Each renderer uses:

- rounded cards
- accent fills and borders
- metadata chips
- progress indicators
- hover and selected treatments

### Item-specific accents

Item cards can visually reflect:

- current item state
- progress status
- item type
- multi-selection status
- marquee-candidate status

### Branch controls

- package and section nodes get circular plus/minus controls
- controls sit just outside the right edge of the node
- they trigger collapse and expand

## Mouse behavior

### Left click

- selects the clicked node
- clicking branch control toggles collapse instead

### Ctrl or Cmd + drag on a node

- enters node-move mode in `authoring`
- if a section is dragged, its child items move with it
- if multiple items are selected, dragging one selected item drags the whole selected item set

### Alt + drag

- enters marquee selection mode in `authoring`
- marquee selection currently collects only item nodes
- package and section nodes are excluded from marquee candidates

### Background drag

- dragging empty space pans the canvas

### Middle mouse drag

- also pans the canvas

### Right click

- in `authoring` mode, opens the context menu for the hit node
- the engine selects that node first

### Double click

- double-click on package or section toggles collapse if it has children
- double-click on a score item emits `open-score`

### Wheel

- zooms toward the current pointer anchor

## Keyboard behavior

The canvas host handles:

- `+` or `=` for zoom in
- `-` or `_` for zoom out
- `0` to fit view
- `?` or `h` to toggle help
- `Escape` to close help
- `Escape` to close the context menu
- `Escape` to leave maximized mode

## Selection model

### Primary selection

- `selectedNodeId` is the primary node
- it drives inspector focus and highlight priority

### Multi-selection

- `selectedNodeIds` stores the whole selected set
- current playlist-builder bulk actions focus on item nodes
- multi-select is mainly intended for batch item operations

### Focus behavior

- `focusNode(nodeId)` selects and centers the node
- the playlist builder uses this to sync the canvas with inspector and outline commands

## Resize and maximize behavior

- `ResizeObserver` triggers `resize()`
- the host can enter maximize mode
- maximize mode uses fixed positioning and large insets
- the engine schedules a resize after maximize toggles

## Interaction details worth preserving exactly

- zoom should anchor under the pointer
- panning should clamp to scene bounds
- selection should survive data refresh when node ids still exist
- branch collapse state should persist across refresh
- manual positions should persist separately from semantic order
- context menu actions should stay node-aware

These details are part of why the current canvas feels responsive and intentional.
