# Codex Rebuild Checklist

Use this checklist before implementing the Blazor version of the events calendar canvas.

## 1. Start from the correct component boundary

- Reuse `zy-canvas-calendar.js` as the real component core.
- Reuse `zy-canvas-primitives.js` as a required dependency.
- Treat `zy-account-events-page.js` as page-specific API wiring only.

## 2. Preserve the current event model

- canonical UTC timestamps
- explicit display timezone
- explicit event timezone
- all-day support
- linked playlists
- checklist counts
- color and read-only state

## 3. Preserve the current view set

- day
- week
- month
- year
- list

Do not replace this with a simpler calendar during the first port.

## 4. Preserve the current interaction vocabulary

- select event
- select date
- drag timed event
- resize timed event
- drag all-day event
- drag month chip
- create timed range
- create all-day span
- double-click create
- keyboard navigation and delete

## 5. Keep responsibilities split correctly

Keep in JavaScript:

- canvas rendering
- date-to-pixel layout math
- hit registry
- pointer interactions
- drag previews
- slot snapping

Keep in Blazor:

- persistence callbacks
- route integration
- application services
- optional future editor/list/panel composition after parity

## 6. Do not casually mix ownership

The current engine owns:

- toolbar
- list
- side panel
- modal editor

Choose one deliberate strategy:

- version one wraps the full widget
- or a later phase splits those surfaces into Blazor

Do not half-port the shell without a clear contract.

## 7. Preserve callback semantics

Keep the callback families intact:

- event create
- event update
- event delete
- playlist search
- playlist link
- playlist clone
- playlist unlink
- date change
- view change
- timezone change
- export request

## 8. Do not regress timezone behavior

- keep UTC canonical
- keep display timezone selectable
- keep editor conversion logic reliable
- avoid introducing server-roundtrip lag into basic canvas interactions

## 9. Delay redesign until after parity

Version one should not:

- rewrite the renderer in SVG
- replace the engine with a third-party scheduler
- remove year or list view
- remove drag previews
- move overlap layout to Blazor

Build a faithful wrapper first. Generalize later.
