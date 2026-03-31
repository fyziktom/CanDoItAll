# Assumptions And Risks

## Assumptions

- The BaseLib stylesheet will continue to be loaded after application-level CSS so downstream apps can layer override rules after the default theme contract.
- A scoped wrapper component is sufficient for runtime theme switching because CSS custom properties cascade through the rendered subtree.
- Existing descriptive enums such as `ButtonStyle.Primary` and `AlertStyle.Danger` should remain the public API, even if internal CSS selectors become canonicalized around `cad-*`.
- CanvasLib can stay functionally separate during this bundle without blocking the new non-canvas theme contract.

## Critical Path Risks

- If the theme contract is expressed only as hard-coded Tailwind utilities, NuGet consumers still cannot override it cleanly and the main goal fails.
- If prefix migration removes `cda-*` and `zy-*` selectors too early, page-level and compatibility components can regress before the route sweep catches it.
- If BaseLib primitives remain partially hard-coded while pages migrate, downstream visual proof becomes misleading because colors will still diverge by implementation path.
- If the public API is reduced to shorthand strings such as `prim` or `sec`, the solution becomes harder to maintain and the architecture regresses into stringly-typed styling.

## Validation Risks

- Runtime theme proof is weak if it only shows separate static pages. The same rendered surface must switch themes during the session.
- Browser validation may be partially blocked if Playwright MCP is unavailable in this environment. The bundle must record that honestly and use the best available CLI/browser proof instead of pretending the gap does not matter.
- Narrow-width regressions are likely once radii, backgrounds, and contrast rules change on tabs, treeview, workbench chrome, and form-heavy editors.
- Page-scoped `.razor.css` files and raw utility markup can bypass the shared contract, so visual proof must include both BaseLib demo surfaces and real app/module routes.

## Reopen Triggers

- Reopen the architecture if consumer override still requires editing BaseLib source files or rebuilding Tailwind inside the consuming app.
- Reopen the architecture if runtime theme switching requires body-level JavaScript hacks that cannot be replaced by a scoped wrapper or app-shell integration.
- Reopen the prefix strategy if alias selectors become unmanageable or if the inventory shows large untouched `zy-*` or `cda-*` hotspots on migrated non-canvas surfaces.
- Reopen a completed implementation subbundle if route screenshots show that a supposed semantic tone still maps to different colors on different surfaces.
