# Normalized Requirements

## Shared Outcome Requirements

- `REQ-01` Unify the non-canvas styles across the solution instead of leaving repeated per-page utility strings and custom CSS islands.
- `REQ-02` Maximize style reusability through Tailwind component-layer classes and BaseLib primitives before allowing page-level one-off markup.
- `REQ-03` Use Tailwind CSS for the absolute most of styling where reasonable, while keeping explicit CSS only where behavior or host integration truly requires it.
- `REQ-04` Reduce duplication and code length without losing functionality, responsive behavior, readability, or affordance.

## Inventory And Taxonomy Requirements

- `REQ-05` Identify every non-canvas raw HTML element such as `div`, `button`, `span`, and similar tags that currently use Tailwind-like classes.
- `REQ-06` Export that census to Excel so patterns can be grouped by frequency and similarity.
- `REQ-07` Unify near-duplicate style variants into one canonical style when the behavior is the same, for example normalizing inconsistent border-radius or spacing choices.

## Tailwind Architecture Requirements

- `REQ-08` Build a well-structured Tailwind system around `Tailwind/input.css` using imported CSS files grouped by responsibility, for example `Controls/buttons.css`, `Forms/*.css`, `Layout/*.css`, and similar.
- `REQ-09` Keep the Tailwind build output wired into `CanDoItAll.Components.BaseLib\wwwroot\css\output.css` and ensure the reorganization does not break the app shell.

## BaseLib And Migration Requirements

- `REQ-10` Review every BaseLib component that already uses Tailwind classes and move it onto the new shared style system when reasonable.
- `REQ-11` Add missing reusable BaseLib components when repeated non-canvas patterns cannot be expressed cleanly with the current library.
- `REQ-12` Analyze all non-canvas custom CSS and replace safe cases with BaseLib components or shared Tailwind-backed classes.
- `REQ-13` For cases that still need specific CSS, prefer Tailwind-backed semantic classes over isolated page-scoped handcrafted CSS when safe.

## Scope And Safety Requirements

- `REQ-14` Do not change `CanDoItAll.Components.CanvasLib` or canvas-adjacent drawing/chrome surfaces in this wave.
- `REQ-15` Work in logical phases and keep bundle documentation, dependency gates, and subbundle proof in sync with the code.
- `REQ-16` Track measurable progress, including replaced raw-element occurrences, unified near-duplicate patterns, and code saved through deletion or consolidation.

## Validation And Closure Requirements

- `REQ-17` Validate each UI-affecting phase with Playwright MCP and screenshots, then fix and revalidate until the shared style library works correctly.
- `REQ-18` Verify that text wraps correctly, components do not overlap, overlays layer correctly, and the resulting UI stays coherent with the app.
- `REQ-19` Re-run analysis near the end to catch missed migration candidates and convert them safely where possible.
- `REQ-20` Reopen the original prompt at closure and answer the mandatory step `0` questions with facts, not optimism. If the answers are weak, continue improving before exit.
