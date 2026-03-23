# Primitives And Styles

This document covers the two supporting layers behind the calendar engine:

- `C:\repositories\zyphonote-web\src\assets\js\zy-canvas-primitives.js`
- the injected style block inside `C:\repositories\zyphonote-web\src\assets\js\zy-canvas-calendar.js`

## Why this file matters

The calendar engine depends on reusable primitives rather than embedding all math and drawing locally. It also ships its own visual system by injecting CSS at runtime.

For the Blazor port, both of those facts matter:

- primitives should stay shared and JS-owned
- styling should eventually move to a standalone asset, but version one can preserve the injected approach

## `zy-canvas-primitives.js`

This file exports:

- `CanvasSurface`
- `HitRegistry`
- `DateMath`
- `drawMiniMonth`
- `drawTimedGrid`
- `fillRoundedPanel`
- `fitText`
- `wrapText`

## `CanvasSurface`

Responsibilities:

- require a real `<canvas>`
- observe size changes with `ResizeObserver`
- measure display size
- scale backing store by device pixel ratio
- expose `size`
- map browser pointer events to canvas-local points
- clear the surface safely

Important implementation details:

- pixel ratio is clamped between `1` and `3`
- `measure()` falls back to the resize target if the canvas rect is zero-sized
- the calendar uses the surface as its redraw boundary, not a virtual world

## `HitRegistry`

Responsibilities:

- store drawable hit regions
- clear them each frame
- find the topmost region for a pointer position

This is the basis for the calendar's region-driven interaction model.

## `DateMath`

Important helpers include:

- `parseDateKey`
- `formatDateKey`
- `addDateDays`
- `addDateMonths`
- `daysInMonth`
- `startOfMonth`
- `endOfMonth`
- `compareDateKeys`
- `dayOfWeek`
- `startOfWeek`
- `endOfWeek`
- `monthLabel`
- `buildMonthMatrix`
- `todayKey`

These functions keep the calendar's day, week, month, and year views consistent without an external date library.

## `drawMiniMonth`

Responsibilities:

- paint a compact month panel
- highlight selected day and today
- render density dots
- return cell bounds for hit registration

The calendar uses it in:

- week-view sidebar
- year view panels

## `drawTimedGrid`

Responsibilities:

- paint the day or week timed grid
- draw hour labels and slot lines
- highlight current day and selected day
- return layout metrics needed by event rendering

Returned layout data includes:

- `leftAxisWidth`
- `headerHeight`
- `bodyX`
- `bodyY`
- `bodyWidth`
- `bodyHeight`
- `dayWidth`
- `minuteHeight`
- `dayRects`

This returned geometry is the contract the calendar engine uses to place timed events, resize handles, previews, and the current-time line.

## Text and panel helpers

- `fillRoundedPanel` draws rounded cards with optional shadow and stroke
- `fitText` truncates text to width
- `wrapText` wraps text to width and line limits

These are used heavily in the month and event-block rendering paths.

## Runtime-injected styles

The calendar engine injects CSS once using:

- `STYLE_ID = 'zy-canvas-calendar-styles'`
- `injectStyles()`

This style block defines the entire visual shell for:

- toolbar
- body grid
- canvas shell
- list shell
- side panel
- chips
- editor modal
- playlist cards
- inline messages
- busy overlay

## Important styling implications

- The component is visually self-contained.
- A host page can mount it with almost no extra CSS.
- A Blazor wrapper can preserve parity by loading the JS and letting it inject styles.
- A later extraction can move that style text into a static `.css` asset in a shared component library.

## Responsive behavior

The injected CSS includes breakpoints:

- under `1280px`, the body collapses from two columns to one and the panel moves above the canvas
- under `720px`, padding tightens and the editor grid collapses to one column

## Visual design choices worth preserving

- rounded shell and panel surfaces
- light gradient backgrounds
- compact chip system
- large white stage with subtle blue-violet backdrop
- high-contrast editor modal
- soft shadows instead of flat borders only

## Typography note

The DOM shell styles use `Segoe UI` and related sans-serif fallbacks, while canvas drawing helpers use `DM Sans` in some primitives. If the Blazor port wants exact visual parity, typography should be reviewed and unified intentionally rather than accidentally changed.

## Recommendation for Blazor

Version one:

- keep primitives in JavaScript
- allow runtime style injection

Version two:

- extract styles to a standalone stylesheet
- keep primitives in a dedicated JS module shared by the Blazor component
