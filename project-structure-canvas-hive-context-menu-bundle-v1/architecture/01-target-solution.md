# Target Solution

## End State

- The root node-context layer renders as a compact honeycomb rather than a loose ring, with a central hub label and a clearly readable first ring of six actionable hexagons.
- The first ring is driven by deterministic action ordering from the shared project-structure catalog or adapter layer rather than by ad hoc browser-only rearrangement.
- The runtime geometry uses a tighter axial-coordinate layout that respects actual hex dimensions so adjacent tiles visually share edges and the overlay bounds remain correct.
- Submenus continue using the honeycomb family where it helps, but their origins and bounds are recalculated so they attach cleanly to the parent tile without clipping or visual pileups.

## Boundaries

- Preserve the existing action model and shortcut metadata instead of introducing a second menu system.
- Preserve current hexagon visual identity, tone colors, glyphs, and shortcut affordances; this is a composition pass, not a thematic redesign.
- Avoid broad rewrites of unrelated canvas selection, composer, or viewport logic.

## Design Thesis

- The menu should feel like a compact command cluster: a central orientation point, one memorably ordered first ring for the most common actions, then a surrounding hive for the rest.
- The visual win comes from adjacency, predictable clockwise scanning, and reduced dead air, not from copying the game screenshot’s materials or lighting.

## Technical Approach

1. Reorder node actions in the adapter layer so first-ring actions are explicit and deterministic.
2. Introduce or refine a honeycomb offset generator whose horizontal and vertical steps are derived from hex metrics instead of loose spacing constants.
3. Use the new offsets for the root node-context layer and any relevant submenu layers.
4. Tune label sizing, hex metrics, and bounds calculations together so compaction does not create unreadable text or clipped overlays.
