# Current state

## Static verdict

This branch is **not yet a safe architectural base** for the next large connector/plugin wave.

## Why

The refactor improved local seams, but the deepest repeated blockers are still present:

- Workbench still persists synchronized cross-module projection nodes and links as a second truth.
- The node carrier is still too broad instead of becoming a stable carrier plus typed facets/bindings.
- Node-kind semantics are still fragmented across enum values, subtype strings, UI catalog definitions, editor logic, and CRM/HR role checks.
- Reclassification still silently mutates the current row instead of writing transition history.
- Hierarchy is still duplicated between explicit parent assignment and generic link rows.
- Providers/resources/connectors are still modeled as a closed enum/switch seam.
- There is still no hard architecture closure mechanism that prevents these repeated blockers from coming back.

## Good progress that should be preserved

- ProjectNodeReference exists and improves the cross-module boundary shape for node references.
- CRM/HR party metadata on the structure page is closer to projection-only display summaries instead of being the canonical store.
- Delete and move subtree compensation tests exist, so failure paths are at least visible and not fully implicit.
- ProjectStructureInvariantService blocks user-authored generic hierarchy links and enforces hierarchy cycle checks.
- Workbench view-state persistence is still separated from the main node storage tables.

## Runtime limitation

`dotnet` is not installed in this environment, so this review could not complete `build / test / run`. That runtime validation remains mandatory before declaring the refactor done.
