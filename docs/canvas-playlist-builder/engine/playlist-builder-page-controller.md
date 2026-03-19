# Playlist Builder Page Controller

This document covers `C:\repositories\zyphonote-web\src\assets\js\zy-playlist-builder-page.js`.

This file is not the canvas engine. It is the playlist-builder orchestration layer that binds together:

- playlist metadata
- normalized manifest state
- score library data
- the canvas controller
- the inspector panel
- the outline editor
- page tabs and scroll targets

## What this controller owns

- Bootstrapping from `window.ZyPlaylistBuilderPageData`
- Manifest normalization and reindexing
- Playlist-specific commands such as add block, add song, duplicate song, move song, and remove selection
- Rendering inspector HTML
- Rendering outline HTML
- Rendering score library HTML
- Syncing the hidden manifest textarea
- Persisting canvas UI state to `sessionStorage`
- Mapping canvas actions into playlist-specific mutations

## Helper functions

### General helpers

- `asText`
- `toInt`
- `safeObject`
- `safeArray`
- `escapeHtml`
- `escapeSelector`
- `clone`
- `nextKey`

These functions make the rest of the file defensive and string-driven.

### View helpers

- `prettyDuration`
- `activatePageTab`
- `scrollToSection`
- `focusNode`
- `focusBasics`
- `focusLibrary`

These functions coordinate the builder tab, smooth scrolling, and node focus.

### Storage helpers

- `readJson`
- `writeJson`
- `loadCanvasUiState`
- `saveCanvasUiState`

These wrap `sessionStorage` and persist per-playlist canvas state.

## Manifest and score helpers

### Score access

- `scoreEntry(scoreId)`
- `openScoreDetail(scoreId)`

`openScoreDetail` opens `account-score-detail.php` in a new tab.

### Duration and summary helpers

- `itemDuration(item)`
- `blockDuration(section)`
- `manifestSummary(current)`

These derive counts and total durations used in the outline and inspector.

### Normalization and indexing

- `normalizeManifest(source)`
- `reindexManifest()`

`normalizeManifest` is important because it defines the shape the rest of the builder expects. It also silently drops invalid items that have no `scoreId`.

## Manifest mutation functions

### Section-level mutations

- `addBlock(afterBlockKey)`
- `moveBlock(blockKey, direction)`
- `removeBlock(blockKey)`

These mutate `manifest.sections`, then call `reindexManifest()` and `syncState()`.

### Item-level mutations

- `addScoreToBlock(scoreId, blockKey, afterItemKey)`
- `moveItem(itemKey, direction, options)`
- `removeItem(itemKey)`
- `duplicateItem(itemKey)`

These operate inside section item arrays and keep the playlist builder selection coherent.

### Selection-level mutations

- `removeSelection()`
- `duplicateSelection()`

These support bulk operations from multi-select canvas interactions.

Important limitation:

- bulk selection is effectively item-centric
- section and package nodes are not part of marquee multi-select in the current engine

## Canvas integration points

### Context action creation

`buildContextActions(node)` translates the generic canvas node into playlist-specific actions.

Package node actions:

- edit playlist basics
- add block
- open score library

Section node actions:

- edit block
- add block after current
- add song into block
- remove block

Item node actions:

- edit song
- add song after current
- duplicate song
- open score detail
- remove song

### Canvas action handling

`handleCanvasAction(event)` handles events emitted by the generic canvas engine, especially:

- `context-action`
- open score workspace requests
- add block
- add score
- duplicate node
- remove node

### Canvas rendering

`renderCanvas()` either creates the canvas controller or pushes updated data into the existing one.

The important call is:

```js
window.ZyLearningPackCanvas.create({
  canvas,
  host,
  manifest,
  packageMeta,
  scoreMap,
  typeLabels,
  mode: 'authoring',
  ui,
  buildContextActions,
  callbacks
});
```

This means the page controller is responsible for:

- initial engine creation
- providing playlist labels
- pushing updated data after mutations
- receiving selection and action callbacks

## Render pipeline owned by the page controller

### Inspector rendering

- `renderEmptyInspector()`
- `renderBulkInspector(selection)`
- `renderPlaylistInspector()`
- `renderBlockInspector(node)`
- `renderItemInspector(node)`
- `renderInspector(selection)`

The inspector is fully HTML-string-driven in the current implementation. It is not a component system.

### Outline and library rendering

- `renderManifestEditor()`
- `renderLibrary()`

These render:

- playlist outline
- block summaries
- song lists
- score search results
- add-song commands

### Main synchronization

- `syncManifestInput()`
- `syncState()`

`syncState()` is the central refresh function. It updates the hidden JSON payload, repaints the canvas, re-renders the outline, and re-renders the score library.

## Input and click handling

### Inspector handling

- `handleInspectorInput(target)`
- `handleInspectorAction(action, button)`

Inspector changes update the in-memory manifest first, then trigger sync.

### Event listeners

The controller wires event listeners for:

- inspector `input`
- inspector `change`
- inspector `click`
- outline `click`
- score library `click`
- page tab `click`
- score search `input`
- add block button `click`
- focus first score button `click`
- document-level copy-link `click`

## Public API exposed to the page

At the end of the file, the controller exposes:

```js
window.ZyPlaylistBuilderPage = {
  getManifest: function() { ... },
  getCanvasController: function() { ... }
};
```

This is minimal, but it is enough for debug tooling and future integration tests.

## Guidance for the Blazor port

- Do not port this file line-for-line.
- Split responsibilities into Blazor components and .NET services.
- Keep the manifest model strongly typed in C#.
- Keep canvas rendering and hit testing in JavaScript.
- Move inspector, outline, and library UI out of HTML-string rendering and into Razor components.
- Preserve the current command vocabulary so the first port can match current behavior exactly.
