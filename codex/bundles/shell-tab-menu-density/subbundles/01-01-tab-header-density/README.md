# 01-tab-header-density

## Status

- `Completed`

## Objective

- Make the large-desktop shell workbar keep tabs, tab search, tab overflow controls, and top-bar status badges on one row to save page height.

## Covered Inputs

- `N001`: tab search moves to the same row as tabs at the end.
- `N002`: status/stat badges move to the same row as tabs.
- `R001`, `R002`.

## Prerequisites

- Bundle preparation gate has passed.
- Source files listed below exist and still own the shell/tab layout.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppTabStrip.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutTopBar.razor
- C:\repositories\CanDoItAll\Tailwind\navigation\tabs.css
- C:\repositories\CanDoItAll\Tailwind\navigation\workbench-shell.css
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AppTabStripTests.cs

## Deliverables

- Large-desktop workbar wrapper and tab strip classes that force one-row alignment only at the large desktop breakpoint.
- Top-bar/status container class that can participate in the same large desktop row.
- Targeted component-test coverage or equivalent markup proof for the new row markers.

## Dependency Impact

- `02-02-sidebar-overflow-continuation-menu` may touch the same shell CSS, so this phase must leave clear class names and avoid broad shell rewrites.
- `03-03-validation-and-closure` depends on this phase for browser proof of reduced shell height.

## Validation Depth

- `UI and component-test foundation`.

## Implementation Steps

1. Add explicit shell workbar and tab-strip row classes without changing mobile navigation.
2. Keep smaller breakpoints stacked or wrapping.
3. Add or update component tests for the row/search/status markers.
4. Record proof in the execution report.

## Scope Exceptions

- This phase does not redesign tab menu contents or sidebar overflow.

## Do Not Do

- Do not change tab state semantics, close/pin/sleep actions, recent-tab behavior, or mobile navigation.
- Do not apply desktop-only no-wrap behavior below the large desktop breakpoint.

## Acceptance Checklist

- Large desktop classes exist for a single workbar row.
- Search, overflow count, and reopen controls remain functional.
- Top-bar status badges remain rendered with their existing data.
- Smaller layouts can still stack safely.

## Proof Required

- Targeted component test or markup assertion for the new row classes and search/status rendering.
- Tailwind rebuild after CSS changes.
- Browser proof on `/processes` or equivalent large desktop route.

## Browser Validation Logging

- Route: `/processes`.
- Viewports: desktop shell breakpoint at or above 1280px width, plus one narrower width below 1280px.
- Actions/assertions: inspect row alignment, search visibility, status badge visibility, no text overlap.
- Screenshot paths: record in `reviews/01-execution-report.md`.
- Review questions: does search sit at the end of the tab row, do badges sit in that same row, and does narrower layout remain safe?

## Progression Gate

- `01` passes when the large-desktop row structure is implemented, targeted proof exists, and no smaller layout regression is observed or suspected.

## Suggested Agent Prompt

```text
Implement this subbundle only. Compact the tab/header row for large desktop using the existing shell/tab components and Tailwind patterns. Preserve smaller layouts and tab behavior, add targeted proof, update the execution report, and stop if the large desktop row cannot be proven.
```
