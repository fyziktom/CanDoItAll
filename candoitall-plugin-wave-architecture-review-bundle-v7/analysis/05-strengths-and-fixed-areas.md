# Strengths and fixed areas

The branch is not a failure. Several changes are useful and should survive the next refactor wave.

- ProjectNodeReference exists and improves the cross-module boundary shape for node references.
- CRM/HR party metadata on the structure page is closer to projection-only display summaries instead of being the canonical store.
- Delete and move subtree compensation tests exist, so failure paths are at least visible and not fully implicit.
- ProjectStructureInvariantService blocks user-authored generic hierarchy links and enforces hierarchy cycle checks.
- Workbench view-state persistence is still separated from the main node storage tables.
