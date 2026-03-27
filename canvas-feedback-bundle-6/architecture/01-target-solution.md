# Target Solution

## Interaction Model

- Progress preset icons render their distinguishing text inside the ring center:
  - `0%`, `10%`, `20%`, ... `100%`
  - `N/A`
  - `Start` keeps the play state without center text
- Marker presets grow to the same larger visual family as progress presets so glyphs and spacing stay legible.
- Nested submenu opening is delayed by a visible hover-progress indicator that completes at about `500ms`.
- Leaving the action before the delay completes cancels the indicator and does not open the child layer.

## Layout Model

- Progress and marker submenu actions use larger hex metrics than today.
- Compact-ring layout is replaced or extended into a staggered honeycomb pattern with alternating rows, so neighboring hexes read as a hive rather than a simple ring.
- Nested layer origin resolution reserves the toolbar band and clamps all submenu bounds into the visible canvas host region.

## Proof Model

- Focused browser coverage must verify:
  - center text is rendered inside progress submenu icons
  - progress and marker submenu items do not overlap
  - nested submenu items stay below the toolbar and within the host
  - submenu loading indicator appears before the layer opens
  - final submenu composition matches the intended hive-style staggering closely enough to inspect in screenshots
- Browser screenshots are required for the default submenu state and the delayed nested-layer state.
