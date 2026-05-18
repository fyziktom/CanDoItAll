# Assumptions And Risks

## Assumptions

- Large-screen density is the priority for this bundle; medium and mobile tuning are intentionally out of scope unless a build or large-screen layout issue exposes a blocker.
- The compact badge text can be short (`Value Label`) while tooltip text carries the longer helper detail.
- Existing page descriptions can be hidden by `PageHeader Compact="true"` where the header is converted to a minimal control strip.

## Risks

- Moving first-screen summary data into the page header can create crowded rows on pages with many stats and actions.
- Tooltip proof is timing-sensitive because the requested 2-second delay means Playwright checks must wait long enough before asserting visible overlay content.
- Replacing stat tiles broadly could disturb pages that used tile height as visual rhythm rather than metadata.

## Critical Path Risks

- If the shared compact stat/action primitives are wrong, every migrated page inherits bad tooltip timing, spacing, or accessibility.
- If the page-header stats slot wraps poorly at 1600px, later page migrations cannot satisfy the one-row large-screen goal.

## Validation Risks

- Screenshots alone may show compact layout but not prove delayed tooltip behavior.
- A build can pass while CSS output is stale if Tailwind/BaseLib generated CSS is not rebuilt or checked.

## Reopen Triggers

- Any migrated page header overflows horizontally or wraps into excessive height at a large-screen viewport.
- Any compact stat/action tooltip appears immediately instead of after about 2 seconds.
- Any page still shows a top-level large `SummaryTile`, `MetricCard`, or stat card in a header/tab summary surface after the sweep.
- Browser proof cannot load representative routes after implementation.
