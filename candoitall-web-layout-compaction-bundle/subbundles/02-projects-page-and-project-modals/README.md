# projects-page-and-project-modals

## Status

- `Completed`

## Objective

- Resolve the user-reported projects density problem by compacting the projects route, aligning search and filters on one large-screen row, reducing wasted board/header height, and tightening the project/database modal families.

## Covered Inputs

- `ART-02` projects screenshot
- Request note about same-row search, filters, and reset
- Request note about using `?` help affordances for verbose helper text
- Request note about analyzing modals as part of the same initiative

## Prerequisites

- `subbundles/01-shell-foundations-and-layout-primitives` must be complete and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectModalHost.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectHierarchyModal.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectModalHost.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\Tailwind\navigation\page-header.css`
- `C:\repositories\CanDoItAll\Tailwind\forms\fields.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\workbench-shell.css`

## Deliverables

- Projects page header and board intro compressed for large screens.
- Projects search, status, project, link, and reset controls arranged into a single wide-row filter toolbar.
- Secondary board helper copy moved behind a compact help affordance if still useful.
- Project modal host and hierarchy modal shells tightened.
- Shell database modal and active-database top-bar region compacted without reducing clarity.

## Dependency Impact

- This subbundle becomes the reference implementation for list/detail density and modal cleanup in later phases.
- Weak proof here would leave the original complaint unresolved and undermine confidence in the broader initiative.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Rework the projects page header and board entry sequence so controls arrive faster than descriptive copy.
2. Recompose the board action and filter area so large-screen search, all selects, and reset fit on one row.
3. Remove or relocate non-essential board copy behind a help affordance where it saves meaningful height.
4. Revisit the board height strategy so the route uses the viewport intentionally without brittle fixed subtraction.
5. Tighten project modal host, hierarchy modal, and shell database modal shells and action rows.

## Scope Exceptions

- Do not remove the ability to open dashboard, structure, calendar, hierarchy, or edit flows from the projects route.

## Do Not Do

- Do not introduce route-specific magic breakpoints that fight the shared toolbar behavior from subbundle 01.
- Do not leave the same vertical stacking in place and only shrink font sizes.
- Do not hide critical status or destructive actions behind the new compact help affordance.

## Acceptance Checklist

- `/projects` first-screen content reaches the actionable board controls quickly.
- Search, status, project, link, and reset stay on one large-screen row.
- The board still works in empty-state and populated-state scenarios.
- Project modal and hierarchy modal open states feel denser and remain readable.
- The shell database modal remains usable and unclipped in its open state.

## Proof Required

- Browser proof on `/projects`
- Open-state screenshots for:
  - project create or edit modal
  - hierarchy modal
  - shell database modal
- DOM/text proof that the filter controls are present on the same toolbar row at desktop width

## Browser Validation Logging

- Target route: `/projects`
- Viewports: `1720x1160`, `1280x900`, `768x1024`
- Required browser actions:
  - open `/projects`
  - close or continue past startup database modal when page layout is being judged
  - open new project modal
  - open hierarchy modal
  - reopen database switcher modal from the top bar
- Required screenshot paths:
  - `output/playwright/subbundle-02-projects-large.png`
  - `output/playwright/subbundle-02-project-modal-large.png`
  - `output/playwright/subbundle-02-hierarchy-modal-large.png`
  - `output/playwright/subbundle-02-database-modal-large.png`
- Required review answers:
  - does the page still waste vertical space before the board?
  - are the filter controls inline on desktop?
  - do modals keep all content visible without feeling oversized?

## Progression Gate

- Downstream work may proceed only after `/projects` closes the original complaint in a reviewed desktop screenshot and the three modal families above have open-state proof with no clipping or hidden actions.

## Suggested Agent Prompt

```text
Implement only subbundle 02.
Use the shared layout primitives from subbundle 01 rather than inventing a route-only toolbar pattern.
The primary acceptance bar is the desktop `/projects` screen and its related modal families.
```
