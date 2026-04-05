# Single marker truth
Markers are canonical project semantics in this product, not decorative UI-only state. Therefore marker truth must be single-source.

Recommended end-state:
- persist `MarkersJson` (or a normalized marker table) as the only canonical marker truth,
- compute a primary marker only for display/search-helper purposes outside the persisted node entity,
- delete scalar marker columns from the persisted node carrier.

If query performance later requires a cached primary marker column, treat it as a computed/cache projection, not as a second truth.
