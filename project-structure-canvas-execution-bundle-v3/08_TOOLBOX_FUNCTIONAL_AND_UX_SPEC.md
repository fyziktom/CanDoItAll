# Toolbox functional and UX specification

## Purpose

The toolbox should behave like a compact Visual Studio toolbox, not like a card grid.

A light theme is acceptable.  
Functionality and compact information density are the priority.

## Reference
Use the included reference image:

- `references/visual-studio-toolbox-reference.png`

## Behavior specification

### Accordion behavior
When search is empty:
- only one group is open at a time,
- clicking a closed group opens it,
- clicking the same open group closes it,
- `aria-expanded` must reflect the real state,
- keyboard Enter and Space must toggle the group.

When search is active:
- all matching groups are open,
- non-matching groups are hidden or collapsed,
- manual accordion state is suspended while the search filter is active,
- when search is cleared, restore the last manual open group or the default group.

### Scrolling behavior
- the search box stays sticky at the top,
- the group list scrolls inside the toolbox body,
- wheel input over the toolbox scrolls the toolbox and never zooms the canvas.

### Item activation
A toolbox row click should trigger the create flow immediately.
The row itself is the click target.

## Item row layout

Each toolbox row must be:
- one line only,
- icon on the left,
- label text on the right,
- compact height,
- text truncated with ellipsis when needed.

Do **not** render the description as a second line under the label.

### Description behavior
Move the description to hover metadata:
- baseline implementation: `title` attribute,
- optional enhancement: custom tooltip.

At minimum, the browser hover must expose the description.

## Group header layout

Each group header should include:
- chevron or disclosure icon,
- group label,
- optional compact count badge,
- clear hover/focus style.

Do not use a large card layout.

## Accessibility requirements

- real button for the header toggle,
- `aria-expanded`,
- focus-visible styling,
- Enter/Space keyboard toggle,
- item buttons remain keyboard reachable,
- tooltip/title text must not replace the accessible item label.

## Visual style requirements

### Allowed
- light or neutral theme,
- thin separators,
- compact typography,
- list-like visual density.

### Avoid
- tall cards,
- two-line item rows,
- excessive chip/badge clutter,
- dashboard-style panels inside the toolbox.

## Suggested CSS behavior

- row height in the roughly 26-32px range,
- icon column fixed width,
- label nowrap + ellipsis,
- sticky search box,
- section body list with no extra card wrappers.

## Mandatory browser proof

Codex must add browser validation for:
1. click collapsed group -> expands,
2. click expanded group -> collapses,
3. search filter -> matching groups shown correctly,
4. rows remain single-line,
5. hover shows description metadata,
6. wheel over toolbox scrolls toolbox and does not zoom scene.

## Important non-goal

Do not move the toolbox into the main scene canvas.

That would:
- hurt maintainability,
- hurt accessibility,
- not solve the real dense-scene performance bottleneck.
