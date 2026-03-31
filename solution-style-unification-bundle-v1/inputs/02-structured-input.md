# Structured Input

## Core Objective

- Replace the current mixture of repeated raw Tailwind utility strings and page-scoped custom CSS with a deliberate, reusable non-canvas styling system that is centered on Tailwind component-layer imports and BaseLib primitives.

## Hard Constraints

- Do not change `CanDoItAll.Components.CanvasLib`.
- Do not change canvas-adjacent drawing or canvas-host chrome in this refactoring wave.
- Do not remove or weaken functionality in the name of cleanup.
- Prefer the smallest safe change over wide speculative redesign.

## Source Artifacts

- The raw prompt preserved in `inputs/00-original-request.md`
- The current Tailwind entry point and compiled output
- The BaseLib component library, app shell, non-canvas modules, and test projects
- The generated census workbook `C:\repositories\CanDoItAll\output\spreadsheet\style-census-initial.xlsx`

## Input Coverage Signals

- The prompt uses absolute language such as `all`, `must`, and explicit mandatory steps. The bundle preserves that scope and records only one justified narrowing: canvas-related scope is excluded exactly as requested.
- The user explicitly requires Excel output, Tailwind imports, BaseLib alignment, Playwright proof, screenshot review, progress metrics, and an honest end-of-work audit.
- The user explicitly allows new BaseLib components when reuse demands them.

## Dependency And Sequencing Signals

- The inventory and taxonomy must land before shared CSS architecture, otherwise later abstractions are guesswork.
- The Tailwind import architecture must land before BaseLib and page migration, otherwise every later edit reopens the same shared-style questions.
- BaseLib alignment must land before wide page migration, otherwise modules will keep recreating repeated patterns.
- Final closure depends on browser proof and refreshed metrics, not only build success.

## Validation Expectations

- Rebuild Tailwind output after each architecture or shared-style change.
- Run targeted `dotnet build` validation after each code phase.
- Measure replaced occurrences, unified families, and code/CSS reduction with facts.
- Reopen earlier phases if later proof shows the foundation was weak.

## UI Validation Strategy

- Use Playwright MCP for all UI-affecting subbundles.
- Start with a large-screen pass on representative routes, then follow with narrower-width passes where layout changes.
- Review screenshots for text readability, overlap, clipping, spacing coherence, responsive wrapping, and overlay layering.
- Keep tuning until the shared style library behaves correctly on the validated routes.

## Browser Validation Analytics

- Log route, viewport, Playwright actions, screenshot paths, and result per subbundle in `reviews/01-execution-report.md`.
- Treat any executed UI subbundle without real Playwright interaction and screenshots as incomplete.

## Working Assumptions

- “Across the solution” means all browser-visible non-canvas surfaces in `src`, including safe sandbox catalog pages, while excluding CanvasLib and canvas-host surfaces.
- Visual parity allows minor alignment normalization when two near-duplicate styles can be safely unified without reducing readability or affordance.
- Existing BaseLib compatibility components may remain as wrappers if that avoids wider churn during this wave.

## Primary Risks

- Missing a high-frequency style family during the census would weaken every downstream phase.
- Shared Tailwind architecture regressions could invalidate later browser proof across many routes.
- Over-aggressive custom CSS removal could break wrapping, density, or layering in subtle ways that only show up in the browser.
