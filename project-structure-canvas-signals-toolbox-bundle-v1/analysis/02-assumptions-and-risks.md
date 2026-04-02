# Assumptions And Risks

## Assumptions

- The new floating toolbox applies to the current selection. When exactly one node is selected, the window should speak in singular language; when multiple nodes are selected, the same actions may apply in batch.
- The single-marker database fields remain as the compatibility surface for existing consumers, while the full additive marker set lives in `MetadataJson`.
- The signals toolbox should include markers, progress, priority, and clear/reset helpers as the initial `few more things` scope unless execution reveals an existing typed signal category that fits better.

## Critical Path Risks

- If the compatibility bridge between metadata markers and legacy single-marker fields is weak, existing canvases or summaries may silently regress.
- If only the context-menu path becomes additive but node rendering still shows one marker, the shipped behavior will feel broken.
- If the toolbox opens without a selected node and the empty-state guidance is unclear, the new surface may feel dead or confusing.

## Validation Risks

- Marker glyph enlargement must be judged in the real browser because emoji-like glyphs such as thumbs-up and car do not scale uniformly.
- The floating toolbox is an overlay, so proof must check open-state clipping, layering above the canvas chrome, and behavior at narrower widths.
- Multi-marker proof must verify both additive storage and visible rendering on the selected node.

## Reopen Triggers

- Reopen subbundle `01` if later browser proof shows only one marker surviving after repeated marker applications.
- Reopen subbundle `02` if the toolbox layout clips content, loses selection context, or applies actions to the wrong node set.
- Reopen any closed phase if compatibility consumers still read only the old marker fields and ignore the new primary-marker sync.
