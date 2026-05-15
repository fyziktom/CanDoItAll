# Large Screen Visual Workspace Refresh

This initiative bundle prepares a phased visual refresh for the CanDoItAll Blazor app. The target is a large-screen B2B workbench that uses more of the available desktop width, collapses the main menu by default, moves database and settings controls out of the page header, and replaces oversized explanatory chrome with concise navigation, tree views, tooltips, and dialogs.

## Profile

- `initiative`

## Mission

- Improve the professional visual quality and working-space efficiency of the application for large PC screens, using the Economy Simulator reference screenshots as the design benchmark and BaseLib/Tailwind component mechanisms as the implementation path.

## Outcome Contract

- Requested outcome: a large-screen-first CanDoItAll UI with compact default navigation, minimal menu labels, right-side tooltips, bottom menu settings/database actions, a database info flyout with copy support, tree-driven large lists, wider page workspaces, and page-by-page density improvements.
- Hard constraints: do not spend implementation time tuning small or medium screens; do not add page-local custom CSS; prefer BaseLib/CanDoItAll shared component parameters, enum variants, and Tailwind classes; use Radzen only if the target project already uses it, and this repo scan found no Radzen usage.
- Evidence required before closure: large-screen Playwright proof for every changed route, open-state proof for collapsed-menu tooltips and database flyout, before/after screenshots, route-by-route screenshot review against the Economy reference, relevant bUnit/unit tests, and a clean targeted build/test pass.
- Known blockers or explicit scope exceptions: mobile and tablet polish is intentionally out of scope; generated `imagegen` mockups are planning evidence only and never count as shipped proof.

## Bundle Layout

- `inputs/` raw request, copied reference screenshots, and structured input
- `inputs/page-inputs/` real implementation page/tab/dialog inputs and UX-flow descriptions
- `analysis/` current repo state, assumptions, risks, validation risks, and reopen triggers
- `requirements/` normalized, testable requirements
- `architecture/` target solution and implementation boundaries
- `plan/` execution order, dependency map, critical foundations, and phase gates
- `traceability/` requirement-to-subbundle mapping and raw-note closure plan
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `inventories/` route and source inventories
- `evidence/` planning evidence, including `imagegen` proposal output
- `reviews/` self-review and execution report skeleton

## Recommended Execution Order

1. `subbundles/00-01-page-function-inputs-and-imagegen-proposals`
2. `subbundles/00-02-baselib-desktop-shell-overlay-primitives`
3. `subbundles/00-03-baselib-tree-detail-tab-dialog-primitives`
4. `subbundles/01-01-design-baseline-imagegen-and-route-inventory`
5. `subbundles/02-02-shared-shell-navigation-and-database-controls`
6. `subbundles/03-03-tree-driven-project-process-and-workflow-surfaces`
7. `subbundles/03-04-process-live-workflow-tabs-and-dialogs`
8. `subbundles/04-04-core-workspace-page-density-pass`
9. `subbundles/04-05-core-prompts-plugins-settings-tabs-and-dialogs`
10. `subbundles/05-05-supporting-module-page-density-pass`
11. `subbundles/05-06-crmhr-operations-tabs-and-dialogs`
12. `subbundles/06-06-large-screen-proof-repair-and-closure`

## Dependency And Validation Map

- The operational dependency map is in `plan/01-phase-plan.md`.
- Critical foundations are subbundles 00-01, 00-02, 00-03, 01, 02, and 03. If any produces weak proof, downstream visual review is untrustworthy and must stop.
- Route inventory and page proposals must be kept current in `inventories/01-scope-inventory.md` as execution discovers stale pages or hidden route states.
- Reusable component candidates must be kept current in `inventories/02-reusable-baselib-component-candidates.md`.
- Browser analytics, screenshot paths, gate results, and raw-note closure rows must be written to `reviews/01-execution-report.md`.

## Validation Summary

- Bundle preparation status: `Ready for implementation`
- Prepared-stage validator: `Passed 2026-05-15 UTC after page-input/proposal repair`
- Execution status: `Executed with scoped closure 2026-05-15`
- Subbundle gate review: `Recorded in reviews/01-execution-report.md`
- Final closure gate: `Closed for standard shell/components/screens; canvas/WebGL residuals documented as explicit scope exceptions`
- Browser validation analytics: `Passed for representative 1920x1080 shell, projects, processes, workflows, and settings routes`
