# Workbench And Canvas Chrome

Two UI layers surround the canvas:

1. Internal canvas chrome from `zy-learning-pack-canvas.js`
2. Generic workbench helpers from `zy-canvas-workbench.js`

They are related, but they are not the same thing.

## Internal canvas chrome

The canvas engine creates a `CanvasChrome` instance and mounts it inside the host.

This internal chrome includes:

- fit command
- maximize toggle
- help toggle
- zoom in
- zoom out
- zoom range slider
- zoom percentage readout
- bottom-left hint text
- centered help overlay card

The engine drives that chrome by calling:

`CanvasChrome.prototype.sync(mode, zoom, helpOpen, isMaximized)`

This is tightly coupled to the engine state and should stay packaged with the JS canvas module.

## Generic workbench helpers

`C:\repositories\zyphonote-web\src\assets\js\zy-canvas-workbench.js` exports:

- `createContextMenu(host, options)`
- `createToolbar(host, options)`
- `createRibbon(host, options)`
- `createDock(host, options)`

## Context menu

The current playlist builder uses only the context menu helper.

Behavior:

- rendered as floating menu inside the host
- item model includes icon, label, description, tone, and id
- dismisses on outside `pointerdown`
- dismisses on `Escape`
- clamps its final position so it stays inside the host

This is the bridge between right-click canvas actions and user-visible commands.

## Toolbar

The toolbar helper supports:

- grouped commands
- optional overflow menu
- left or right positioning
- alignment control
- sticky behavior

The current playlist builder page does not use it, but it is useful for a future Blazor shell.

## Ribbon

The ribbon helper supports:

- brand area
- kicker
- title
- tabs
- grouped commands per tab
- sticky positioning

This is useful if the future Blazor editor becomes a larger authoring workspace with mode-specific command groups.

## Dock

The dock helper supports:

- left or right docking
- tab strip
- pinned state
- collapsed state
- body content
- footer content

This is relevant for a future Blazor implementation if the inspector, library, assets, and layers become dockable panels instead of fixed page sections.

## CSS ownership

The workbench helpers rely on `input.css` for all layout and visual identity. Important class families include:

- `.zy-canvas-context-menu*`
- `.zy-canvas-toolbar*`
- `.zy-canvas-ribbon*`
- `.zy-canvas-dock*`
- `.lp-canvas-*`

## Guidance for the Blazor version

- Keep context menu behavior in JavaScript for the first version.
- Expose command execution back to Blazor via callbacks.
- Defer toolbar, ribbon, and dock adoption until the core wrapper is stable.
- Do not mix Blazor-rendered overlay controls and JS-rendered overlay controls without a clear ownership model.

Recommended ownership model:

- JavaScript owns canvas, hit testing, viewport math, and tightly coupled overlay chrome.
- Blazor owns editor forms, persistence, domain models, route state, and high-level layout composition.
