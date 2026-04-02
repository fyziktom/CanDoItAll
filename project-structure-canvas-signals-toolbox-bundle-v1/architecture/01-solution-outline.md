# Solution Outline

## Data Contract

- Extend `ProjectObjectMetadataEnvelope` with a marker-set payload that stores ordered marker entries.
- Treat the metadata marker list as the source of truth for additive markers.
- Keep `MarkerIcon`, `MarkerTone`, and `MarkerLabel` synchronized to the primary marker so existing readers continue to work.

## Page And Window Contract

- Add a second project-structure floating window dedicated to node signals.
- Reuse the existing `CanvasFloatingWindow` contract and toolbar-toggle pattern already used for the blocks toolbox.
- Show explicit selection context in the window header or summary area.

## Rendering Contract

- Enlarge the glyph inside marker preset menu badges with CSS only, leaving badge width and height untouched.
- Upgrade both DOM and canvas node renderers to paint more than one marker badge, with a compact overflow strategy if the marker count grows.
- Update selection summaries so the primary detail card no longer implies only one marker exists.

## Interaction Contract

- Context-menu marker actions add markers to the set instead of overwriting the entire set.
- `Clear` removes all markers.
- Toolbox marker buttons may toggle active markers off when the same marker is clicked again, but repeated add clicks must never collapse the set back to one marker unless `Clear` or explicit removal is chosen.
