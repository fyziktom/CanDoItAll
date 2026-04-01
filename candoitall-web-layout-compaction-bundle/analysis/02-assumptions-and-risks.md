# Assumptions And Risks

## Assumptions

- Large-screen compaction should improve the first-screen utility of each route without removing required context entirely.
- Hiding secondary helper copy behind a small help affordance is acceptable when the content is instructional rather than status-critical.
- Projects, settings, and the shell top bar are the highest-leverage reference surfaces for the broader cleanup.
- The current workspace may require validating important empty states and open modal states, not only data-rich screens.

## Critical Path Risks

- Shell and scaffold width changes can introduce regressions on every route at once, including workbench-heavy pages.
- Over-aggressive header compaction could remove cues that first-time users still need, especially on Settings and Prompt Gallery.
- Reworking the projects board without improving the shared layout primitives would likely create a one-off solution that the rest of the app cannot reuse.
- Workbench overlays and prompt factory dialogs are custom enough that they may need targeted fixes even after shared dialog work lands.

## Validation Risks

- The Playwright MCP browser is currently blocked in this environment by an `EPERM` failure while trying to create `C:\Windows\System32\.playwright-mcp`.
- Real browser proof is still possible through terminal Playwright CLI, but the execution report must record that fallback honestly.
- The startup database-selection modal appears in the current watch session, so route proof must either include open-state validation or explicitly close it before layout checks.
- Some workbench routes need at least one project record before their structure or calendar surfaces can be validated meaningfully.

## Reopen Triggers

- Reopen subbundle 01 if any large-screen shell change causes new clipping, a collapsed right rail regression, or major misalignment on untouched routes.
- Reopen subbundle 02 if project board controls still wrap into multiple rows at `1720x1160`, or if project/database modals lose readable body space.
- Reopen subbundle 03 if another list/detail route still shows a tall intro stack that could have been collapsed with the new shared patterns.
- Reopen subbundle 04 if any overlay opens partially off-screen, under neighboring chrome, or with unreadable action rows.
- Reopen subbundle 05 if screenshot review exposes spacing, hierarchy, or overflow defects that were missed by DOM-only checks.

