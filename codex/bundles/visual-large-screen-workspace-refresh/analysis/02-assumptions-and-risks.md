# Assumptions And Risks

## Working Assumptions

- The implementation target is the primary Blazor app under `C:\repositories\CanDoItAll\src\CanDoItAll.Web` and module pages loaded by that host.
- Large desktop proof uses a maximized browser or an explicit viewport around `1920x1080`.
- Existing small/medium behavior should not be deliberately broken, but small/medium polish is not a validation target.
- Shared component and Tailwind-source changes are acceptable when they create reusable styling options; page-local custom CSS additions are not acceptable.
- The current material icon based `Icon` component is the app's established icon path; do not introduce a second icon library for this refresh.
- Page inputs in `inputs/page-inputs` are the functional contract for implementation; if source changes, SB00-01 must refresh them before coding continues.
- Accepted `imagegen` proposal boards are planning references for composition only; exact generated labels and domain examples are not authoritative.

## Critical Path Risks

- Shell changes are a critical foundation: if collapsed navigation, topbar removal, or bottom DB actions are wrong, every page-level screenshot becomes misleading.
- BaseLib foundation changes are a critical foundation: if reusable rail, tree/detail, tab, dialog, metric, or toolbar patterns are not strong enough, page teams will drift into one-off CSS.
- Tree conversion can damage behavior if list selections, route navigation, project hierarchy filters, process runtime actions, or workflow version selection are only visually rearranged without preserving state.
- Some pages already use substantial page-local CSS; the bundle forbids adding new one-off CSS, but removing all existing custom CSS is not required unless a touched area must be moved into shared Tailwind/BaseLib.
- `PromptFactoryPage.razor` and `ProjectStructurePage.razor` are large enough that broad cosmetic edits can easily cause functional regressions.
- Database flyout copy behavior may expose sensitive connection details if implemented carelessly; copy text must be intentionally selected and mask secrets.

## Validation Risks

- A screenshot attached without explicit visual review is not proof; every browser screenshot must answer density, hierarchy, clipping, overlay, and reference-similarity questions.
- Open-state overlays are easy to miss. Collapsed-menu tooltips, expanded menu state, database flyout, dialogs, and tree context menus need explicit screenshots.
- Large-screen-only scope can hide accidental mobile regressions; this is accepted only when the regression is outside the desktop path and documented.
- `imagegen` proposals may look polished but not match available component constraints. Implementation must remain grounded in BaseLib/Tailwind capabilities.
- A generated board can cover many tab/dialog states in separate panels, but runtime proof still must exercise each real tab/dialog state after implementation.

## Reopen Triggers

- Reopen subbundle 00-01 if page inputs miss a route-bearing page, a real tab body, a dialog family, or a page-specific visual proposal.
- Reopen subbundle 00-02 if shell implementation requires one-off markup because the shared rail/flyout primitives are insufficient.
- Reopen subbundle 00-03 if page teams need custom CSS to express tree/detail, dense tab, dialog, metric, toolbar, or compact state patterns.
- Reopen subbundle 01 if route inventory misses a route-bearing page, hidden route state, baseline screenshot, or accepted proposal reference.
- Reopen subbundle 02 if any downstream screenshot still shows database switching in the topbar, the old verbose sidebar, clipped shell tooltips, or lost Settings access.
- Reopen subbundle 03 if projects, processes, or workflows still rely only on flat cards/lists where a hierarchy exists.
- Reopen subbundle 03-04 if process, live process, or workflow tab/dialog states are not individually proven.
- Reopen subbundle 04 or 05 if a page remains materially narrower than the available workspace or uses long explanatory copy where tooltip/dialog disclosure would be clearer.
- Reopen subbundle 04-05 or 05-06 if core/supporting tab and dialog states are not individually proven.
- Reopen subbundle 06 if visual repair screenshots are missing, not reviewed against the reference screenshots, or fail overlay/readability checks.
