# Target Solution

## Summary

- Use `MetadataJson` as the source of truth for an ordered marker set while preserving the legacy single-marker columns as a primary-marker compatibility bridge.
- Extend structure and canvas node projections with marker collections so both DOM and canvas renderers can show multiple markers.
- Add a new floating node-signals window using the existing canvas floating-window infrastructure and toolbar toggle rhythm.
- Enlarge only the glyph inside second-layer marker preset badges through CSS and leave badge width and height untouched.

## Why This Shape

- It avoids a database migration for a UI-driven follow-up.
- It keeps the existing system compatible with consumers that still read `MarkerIcon`, `MarkerTone`, and `MarkerLabel`.
- It reuses established workbench window patterns instead of creating a new overlay framework.
