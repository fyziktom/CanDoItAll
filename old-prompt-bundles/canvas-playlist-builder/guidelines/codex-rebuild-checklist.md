# Codex Rebuild Checklist

Use this checklist before implementing the Blazor version of the playlist builder canvas.

## 1. Start from the correct abstraction

- Reuse the generic canvas engine documented in this folder.
- Do not start from the playlist-specific page controller.
- Do not assume this is an arbitrary graph editor.

## 2. Preserve the current scene contract

- one package node
- ordered section nodes
- ordered item nodes
- stable ids based on section and item keys
- separate manual positions for visual offsets

## 3. Preserve the current interaction contract

- left click selects
- wheel zooms under pointer
- empty-space drag pans
- Ctrl or Cmd plus drag moves nodes in authoring mode
- Alt plus drag creates marquee item selection
- double-click toggles package and section collapse
- right click opens node context actions in authoring mode

## 4. Keep responsibilities split correctly

Keep in JavaScript:

- canvas rendering
- viewport math
- hit testing
- drag and zoom behavior
- internal canvas chrome

Keep in Blazor:

- inspector forms
- outline panels
- score library
- persistence
- validation
- save workflow

## 5. Mirror the existing controller API first

The first wrapper should support at least:

- create
- destroy
- setData
- setMode
- setSelection
- focusNode
- fitView
- setZoomPercent
- getState
- resize

## 6. Do not lose persisted state features

Preserve:

- selected node ids
- collapsed nodes
- zoom and pan
- maximize flag
- manual positions
- current item key

## 7. Match current command vocabulary

Preserve or intentionally map:

- edit node
- add section
- add score
- remove node
- duplicate node
- open score detail
- open score workspace

## 8. Delay generalization until after parity

Version one should not add:

- arbitrary graph edges
- unlimited hierarchy depth
- SVG rewrite
- pure C# renderer
- blended reorder and drag semantics

Build a faithful wrapper first. Generalize only after the current behavior is reproduced and documented.
