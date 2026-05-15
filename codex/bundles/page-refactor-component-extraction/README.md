# Page Refactor Component Extraction

This bundle is a coordination and execution package for `page-refactor-component-extraction`.

## Profile

- `initiative`

## Mission

- Refactor the app's long Blazor pages and page-owned components into smaller helper classes and focused Razor components while preserving rendered behavior, routes, events, state transitions, tests, and browser-visible workflows.

## Outcome Contract

- Requested outcome: every app route page is inventoried, necessary helper and component isolations are identified in the workbook checklist, and the implementation proceeds through atomic subbundles with helper extraction before component extraction.
- Hard constraints: preserve behavior, keep strongly typed C# boundaries, do not hide errors through fallback mechanisms, keep page logic explicit, use existing BaseLib and CanvasLib components, and do not replace shared wrappers with raw structural markup.
- Evidence required before closure: prepared and completed bundle validator passes, targeted component/unit tests, `dotnet build`, browser proof for affected routes, screenshot review for changed UI surfaces, and raw request closure rows.
- Known blockers or explicit scope exceptions: the CanDoItAll components MCP returned `Transport closed` during preparation; execution must retry it before adding new structural layout markup and otherwise rely on local BaseLib usage examples.

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
- `inventories/` route and component inventory, including the `.xlsx` checklist
- `templates/` copied subbundle README template

## Recommended Execution Order

1. `subbundles/01-project-structure-node-helpers`
2. `subbundles/03-prompt-factory-canvas-helpers`
3. `subbundles/05-plugin-page-helpers-and-render-fragments`
4. `subbundles/06-crm-hr-page-helper-extraction`
5. `subbundles/07-workspace-settings-helper-extraction`
6. `subbundles/02-project-structure-page-shell-components`
7. `subbundles/04-prompt-factory-page-shell-components`
8. `subbundles/08-process-and-workflow-editor-page-decomposition`
9. `subbundles/09-remaining-route-page-cleanup`
10. `subbundles/10-final-regression-proof-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- Use `inventories/page-refactor-checklist.xlsx` as the human checklist for route inventory, helper candidates, component candidates, and atomic execution status.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, the workbook checklist, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed prepared-stage validator`
- Execution status: `In progress`
- Subbundle gate review: `01, 03, and 05 passed; remaining subbundles pending`
- Final closure gate: `Not started`
- Browser validation analytics: `Seeded`
