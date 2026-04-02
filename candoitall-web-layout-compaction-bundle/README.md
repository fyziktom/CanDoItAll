# CanDoItAll Web Layout Compaction and Modal Efficiency Bundle

This initiative bundle prepares and executes a large-screen-first UI density pass across the CanDoItAll web app. The goal is to reclaim wasted horizontal and vertical space in the shell, main pages, and modal surfaces while preserving the existing component language, predictable Blazor behavior, and typed service boundaries.

## Profile

- `initiative`

## Mission

- Deliver a compact, large-screen-optimized CanDoItAll web workspace where the shell, page headers, summaries, filter rows, and modal shells use the available browser width intentionally, the projects page stops stacking search and filter controls into unnecessary height, verbose helper copy can move behind a small info affordance when appropriate, and shared components become flexible enough that downstream pages do not need per-page layout hacks.

## Bundle Layout

- `inputs/` preserves the raw request, the provided screenshot note, and structured interpretation of the request.
- `analysis/` captures the actual repo state, layout bottlenecks, assumptions, and reopen risks.
- `requirements/` turns the request into explicit, testable layout and proof requirements.
- `architecture/` defines the target large-screen layout strategy, shared-component boundaries, and Tailwind-first implementation rules.
- `inventories/` lists the routes, modals, overlays, and shared components touched by the initiative.
- `plan/` defines the dependency map, critical foundations, and stop gates.
- `traceability/` maps raw inputs and normalized requirements to owning subbundles and proof.
- `shared-prompts/` contains implementation and QA prompts tuned for small-step watch plus browser validation.
- `subbundles/` splits the work into five execution-ready workstreams.
- `reviews/` records bundle self-review and the seeded execution report.

## Recommended Execution Order

1. `subbundles/01-shell-foundations-and-layout-primitives`
2. `subbundles/02-projects-page-and-project-modals`
3. `subbundles/03-list-detail-pages-and-settings-density`
4. `subbundles/04-workbench-and-prompt-factory-overlays`
5. `subbundles/05-browser-proof-and-responsive-polish`

## Dependency And Validation Map

- The dependency map, critical subbundles, and phase gates live in `plan/01-phase-plan.md`.
- The exact closure rules and acceptance checklists live in the individual subbundle README files.
- The browser analytics, gate decisions, and raw-note closure tracking live in `reviews/01-execution-report.md`.

## Validation Summary

- Bundle preparation status: `Prepared and revalidated`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Pass`
- Browser validation analytics: `Completed through playwright-core + Edge fallback with reviewed screenshots for main route and modal families`
