# Assumptions And Risks

## Assumptions

- Non-canvas styling work includes browser-visible surfaces in `CanDoItAll.Components`, `CanDoItAll.Components.BaseLib`, `CanDoItAll.Web`, and the non-canvas modules, plus safe sandbox catalog pages.
- CanvasLib and canvas-host chrome are excluded even if they contain ordinary HTML around the canvas surface.
- The shared styling contract should favor BaseLib components first and semantic Tailwind component-layer classes second. Raw repeated utility strings are the fallback, not the target.
- Small visual normalizations are acceptable when they unify near-duplicate patterns and preserve readability, spacing, and affordance.

## Critical Path Risks

- If the census or taxonomy misses a major repeated family, the Tailwind architecture and migration phases will duplicate the wrong abstraction and force rework.
- If the restructured Tailwind imports regress shared shell or form styling, every downstream page validation becomes untrustworthy.
- If BaseLib primitives change semantics or spacing in a way that is not smoke-tested on dependent pages, the migration phase may appear correct locally while shipping broad regressions.
- If custom CSS is removed too aggressively, text wrapping, responsive stacking, focus states, and overlay layering can break in ways that simple build validation will not catch.

## Validation Risks

- Representative browser proof requires a working non-canvas app state on routes such as `/`, `/projects`, `/resources`, `/prompts`, `/validation`, `/activity`, and `/settings`.
- Some module routes depend on seeded data or persisted workspace state, so browser validation may require setup during execution.
- Tailwind output is generated outside the .NET build. A missed rebuild could create false confidence if code compiles while the shipped CSS is stale.
- Page-scoped `.razor.css` files such as `PromptFactoryPage.razor.css` and `ReconnectModal.razor.css` are large enough that replacement work may need iterative browser passes instead of one-shot edits.

## Reopen Triggers

- Reopen subbundle `01` if later work finds a repeated pattern family that was not inventoried or if the exclusion list proves too narrow or too broad.
- Reopen subbundle `02` if any shared shell, label, field, button, card, or spacing family needs a second abstraction because the initial imported CSS layout was incomplete.
- Reopen subbundle `03` if dependent pages still need repeated raw utilities because the BaseLib component surface remains insufficient.
- Reopen subbundle `04` if browser screenshots show clipping, overlap, mis-layered overlays, broken wrapping, or unstable responsive behavior on any migrated route.
