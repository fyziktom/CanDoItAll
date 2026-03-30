# Assumptions And Risks

## Working Assumptions

- The original legacy bundle documents remain the detailed source audit and task archive for this execution.
- The normalized compatibility layer added under `inputs/`, `analysis/`, `requirements/`, `architecture/`, `plan/`, `shared-prompts/`, and `subbundles/` exists to satisfy the current validator without replacing the legacy material.
- Remaining compatibility helpers outside the active runtime path are acceptable as long as they are not used by the shipped canvas renderer and the regression suite stays green.

## Critical Path Risks

- Reintroducing DOM or SVG scene rendering into the active workbench hot path would reopen the core bundle objective immediately.
- Reintroducing eager state persistence on drag, pan, or selection-heavy flows would reopen the ProjectStructure and PromptFactory regression risk.
- Replacing the centralized asset includes with per-shell manual script lists would reopen the shared-consumer drift risk.

## Validation Risks

- Playwright coverage is the authoritative UI proof. A component-only green run is not enough for closure.
- Any future runtime JS change must keep `npm run canvaslib:verify-assets` green or the generated public asset will drift from source.
- The bundle validator is structure-only. Final truth still depends on the execution report and the full regression pack.

## Reopen Triggers

- A failing Playwright regression in ProjectStructure, PromptFactory, export flows, or benchmark capture.
- A source audit showing active scene layers rendered through DOM node cards, SVG links, SVG minimap, or DOM-clone export.
- A regression that restores unconditional ProjectStructure reload-after-move or eager view-state writes.
