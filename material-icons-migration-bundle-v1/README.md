# Material Icons Migration Bundle

This bundle is a coordination and execution package for `material-icons-migration-bundle-v1`.

## Profile

- `initiative`

## Mission

- Replace Font Awesome delivery and raw text or glyph icon escapes across CanDoItAll with locally hosted Google Material Icons, anchored in the shared component layer, tracked through a workbook-backed census, and closed only after build, browser, and route-level proof.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report
- `inventories/` workbook, CSV exports, scope summary, and token mapping artifacts
- `templates/` copied bundle template for future follow-up waves if the icon migration needs extension

## Recommended Execution Order

1. `subbundles/01-icon-census-tracker-workbook-and-migration-map`
2. `subbundles/02-local-material-icons-foundation-and-shared-renderer-conversion`
3. `subbundles/03-baselib-and-legacy-shared-component-icon-migration`
4. `subbundles/04-non-canvas-app-and-module-icon-adoption`
5. `subbundles/05-workbench-canvas-and-closure-validation`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- Treat subbundles `01`, `02`, and `03` as critical foundations. Later route proof is not trustworthy if the census, local asset delivery, or shared renderers are weak.
- Keep `C:/repositories/CanDoItAll/output/spreadsheet/material-icons-migration-tracker.xlsx` current while execution is happening so remaining work and completed work stay visible.
- Respect the existing dirty worktree in `TreeViewNodeRow.razor`, `ProjectStructureToolboxWindow.razor`, `ProjectStructurePage.razor`, and `ProjectStructurePage.razor.css` during the later Workbench phase.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `In progress`
- Subbundle gate review: `In progress`
- Final closure gate: `Not started`
- Browser validation analytics: `Not started`
