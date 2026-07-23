# dashboard-quick-actions-and-activity-ui

## Status

- `Completed`
- Checkpoint: `AC02 Approved`

## Objective

- Replace the current workbench-oriented Home content with a compact operational dashboard driven only by the validated snapshot contract.

## Success Criteria

- Four compact square quick-action cards show centered icons and titles for Projects, Agents, Live Processes, and Scheduler and navigate to their canonical routes.
- Home shows total tokens and known USD cost, the last five updated projects, and one tabbed activity card for workflow/process active-or-recent rows.
- Initial loading, empty data, refresh errors, and stale-after-failed-refresh states are explicit and distinct.
- Manual refresh forces a new load; a tiny adjacent caption reports snapshot age/remaining automatic-refresh time.
- Automatic refresh stops on disposal, never overlaps page refresh work, and preserves page-owned scrolling with no overlay or nested scroller.

## Covered Inputs

- Requirements `REQ-01` through `REQ-04` and `REQ-12` through `REQ-14`, plus the browser-visible portions of `REQ-15` through `REQ-18`.
- Architecture decision `PSR-04` and compact large-screen composition policy.

## Prerequisites

- Satisfied on 2026-07-22: SB01 is complete and checkpoint `AC01` is approved with typed snapshot and activity-mode contracts available.

## Exact Source References

- `repo://src/App/CanDoItAll.Web/Components/Pages/Home.razor`
- `repo://src/App/CanDoItAll.Web/Components/_Imports.razor`
- `repo://src/UI/CanDoItAll.AppComponents/Components/ResourceCardPicker.razor`
- `repo://src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`
- `repo://tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`

## UI Composition Contract

- Primary surface: `PageScaffold` and compact `PageHeader`; the four quick actions are supporting entry points at the top, followed by a two-track projects/activity body.
- Stats: `CompactStat` values for Total tokens and Known cost (USD); no chart is justified for two scalar totals.
- Organization: `Grid` for the four square actions and desktop body; `SectionCard` for projects and activity; `Tabs`/`TabsItem` for Workflows and Processes; `SelectionListItem` for compact rows.
- Textarea/dialog sizing: N/A; no editor, dialog, menu, tooltip-only workflow, or overlay is introduced.
- First viewport: at `1440x900`, header, totals, all four quick actions, and the start of both operational cards are visible. The document/page remains the only scroll owner.

## Deliverables

- Reusable `QuickActionCard` Razor wrapper and scoped styling in `CanDoItAll.AppComponents`.
- Reworked `Home.razor` with snapshot rendering, tabs, routes, explicit errors, force refresh, countdown, and disposal-safe automatic refresh.
- bUnit tests for quick-action semantics/routes, populated/empty/error states, tabs, row bounds, force refresh, countdown, automatic refresh, and disposal.

## Dependency Impact

- SB03 browser and architecture closure depends on this exact composition. Moving data policy into Home or duplicating the action card invalidates the gate.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical surface: `/` and `/dashboard` desktop Home rendering and interaction.

## Implementation Steps

1. Add and directly test the reusable semantic quick-action wrapper.
2. Replace obsolete Home workbench/project-list dependencies and content with snapshot-only rendering.
3. Add metrics, four actions, recent projects, and the tabbed workflow/process activity card using BaseLib wrappers.
4. Add explicit load/error/stale states, force refresh, low-frequency countdown rendering, automatic refresh, and async disposal.
5. Add component tests and verify canonical navigation URLs.
6. Review Home responsibility/size and pass checkpoint `AC02` before browser closure.

## Scope Exceptions

- Large-screen desktop only; no dashboard-specific mobile/tablet breakpoints.
- No changes to BaseLib itself; the product-specific square navigation wrapper belongs to AppComponents.

## Do Not Do

- Do not add ad-hoc repeated cards, raw data access, workbench state, charts, overlays, nested scrolling, per-second server rendering, or silent empty/error fallback.
- Do not use Radzen or add CSS/package assets; use the installed BaseLib/Common/AppComponents stack.

## Acceptance Checklist

- [x] Exactly four actions exist with the required labels, icons, compact square layout, accessible links, and canonical routes.
- [x] Tokens, known cost, projects, workflows, and processes render from one snapshot and no list exceeds five rows.
- [x] Activity mode text distinguishes active rows from latest fallback rows.
- [x] Force refresh and remaining-time caption are adjacent, accessible, and behavior-tested.
- [x] Old data stays visible only with an explicit stale/error indication after refresh failure.
- [x] Timer/refresh work stops on disposal and page scrolling remains the sole scroll owner.

## Proof Required

- Targeted bUnit/component tests with positive and adversarial cases.
- Diff/source assertion that Home no longer injects `WorkbenchStateService` or `ProjectsService`.
- Component test proof for all routes, tabs, row limits, force-refresh count, automatic expiry, error, and disposal.
- Subbundle validator and checkpoint `AC02` review.

## Browser Validation Logging

- Routes: `/` and `/dashboard`.
- Viewport: `1440x900` only for this product page.
- Before closure, Playwright must verify the four links, metrics, activity tab switch, countdown text, manual refresh reset, populated and empty states, and absence of dialogs/menus/floating surfaces.
- Screenshots: `artifacts/dashboard/dashboard-populated-1440x900.png`, `dashboard-process-tab-1440x900.png`, `dashboard-empty-1440x900.png`, and `dashboard-refresh-error-1440x900.png` when deterministic fixtures permit.
- Review document dimensions, dashboard bounds, first visible project/activity rows, clipping, density, centered icons, focus treatment, and the single page-scroll owner.

## Progression Gate

- `AC02 Approved` on 2026-07-22 after 7/7 Home/QuickActionCard tests and an independent responsibility review. The first review's exact-label and wall-clock timer-proof blockers were corrected before approval. Final browser proof subsequently confirmed four compact 162×162 desktop cards and no lateral overflow.

## Closure Evidence

- `bundle://evidence/SB02/component-behavior-and-ac02.md`
- `bundle://evidence/SB03/home-dashboard-1440x900-viewport.png`
- `bundle://evidence/SB03/home-dashboard-1440x900-populated.png`

## Reopen Triggers

- Reopen SB02 and repeat SB03 browser proof if action count/route/shape changes, Home gains data policy, refresh errors become empty data, timers outlive disposal, a nested scroller/overlay appears, or the first-viewport target fails.

## Suggested Agent Prompt

```text
Implement SB02 only after AC01. Compose the established BaseLib components and one AppComponents quick-action wrapper, keep Home snapshot-only, prove refresh/error/timer behavior with bUnit, update the execution report, and stop before final closure if AC02 cannot pass.
```
