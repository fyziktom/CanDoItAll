# Remaining Route Page Cleanup

## Status

- `Ready`

## Objective

- Apply inventory-driven helper or component cleanup to remaining route pages where the workbook shows a real maintainability payoff.

## Covered Inputs

- `N001`
- `N002`
- `R010`

## Prerequisites

- Helper foundation subbundles for touched areas are completed.
- Workbook rows identify exact files and refactor type.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\Pages\SchedulerPlannerPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab\Pages\TestLabPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation\Pages\ValidationCenterPage.razor`

## Deliverables

- Targeted cleanup for medium-long route pages listed in the workbook.
- Pages marked "reviewed, no edit" when decomposition would add indirection without reducing complexity.
- Workbook status updated for every route page.

## Dependency Impact

- Final raw-note closure depends on this phase because the user asked for each page to be considered.

## Validation Depth

- Inventory-driven page cleanup with targeted tests and browser route smoke.

## Implementation Steps

1. Work through workbook rows in priority order.
2. For each page, either implement the smallest helper/component extraction or mark reviewed/no-edit with reason.
3. Run targeted tests for touched modules.
4. Browser-smoke every touched route.
5. Update workbook row status.

## Scope Exceptions

- Tiny pages with no meaningful helper/component split can be closed as reviewed/no-edit.

## Do Not Do

- Do not refactor pages only to reduce line count.
- Do not create generic abstractions without reuse pressure.

## Acceptance Checklist

- Every route page has a workbook status.
- Touched pages have tests or route smoke proof.
- Reviewed/no-edit pages include a clear reason.

## Proof Required

- Targeted tests for each touched module.
- Browser proof for each touched route.
- Workbook status review.

## Browser Validation Logging

- Routes: inventory-selected touched routes.
- Viewport: `1600x900`; narrow follow-up when layout changes.
- Required actions: navigate and exercise changed visible regions.
- Screenshots: required for visible component extraction.

## Progression Gate

- All route pages are either refactored with proof or explicitly reviewed/no-edit before final closure.

## Suggested Agent Prompt

```text
Implement subbundle 09 only. Use the workbook checklist to clean up remaining route pages one at a time, avoid low-value extraction, run targeted proof for touched pages, and update row statuses.
```
