# CRM HR Page Helper Extraction

## Status

- `Ready`

## Objective

- Extract filters, formatters, editor factories, clone helpers, and small view-model helpers from long CRM/HR pages while preserving sensitive-data behavior and route flows.

## Covered Inputs

- `N001`
- `N003`
- `R005`

## Prerequisites

- Prepared-stage bundle validator passed.
- CRM/HR workbook rows identify which pages need helper extraction.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrDirectoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrCrmPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrWorkforcePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrRecruitingPage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`

## Deliverables

- Helper classes for CRM/HR filters and display builders where methods are pure.
- Editor factory helpers extracted where they do not require page state beyond explicit inputs.
- Nested view models moved to separate files when they are substantial.

## Dependency Impact

- `09-remaining-route-page-cleanup` depends on this phase to reduce logic before any CRM/HR component splits.

## Validation Depth

- Cross-route helper validation with sensitive-data proof.

## Implementation Steps

1. Start with one CRM/HR page at a time.
2. Extract only pure helpers with explicit typed inputs.
3. Preserve sensitive-data handling and audit/navigation flows.
4. Run targeted component or Playwright tests for touched routes.
5. Update workbook statuses after each page.

## Scope Exceptions

- Do not move privacy or authorization decisions out of their current service/domain boundaries.

## Do Not Do

- Do not combine all CRM/HR UI component splits into this helper phase.
- Do not change route navigation or selected party/account behavior.

## Acceptance Checklist

- Touched CRM/HR pages have reduced helper density.
- Filter behavior and editor model generation remain stable.
- Sensitive-data tests still pass.

## Proof Required

- Relevant `CrmHr*` Playwright tests for touched routes.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter CrmHr`
- Additional helper tests if branch-heavy factories move.

## Browser Validation Logging

- Routes: `/crm-hr/directory`, `/crm-hr/crm`, `/crm-hr/workforce`, and any touched CRM/HR route.
- Viewport: `1600x900`.
- Required actions: filter/search, select entity, open edited regions, verify sensitive-data affordances.
- Screenshots: required for visible extraction.

## Progression Gate

- CRM/HR tests and route proof for touched pages pass before `09` can refactor remaining route shells.

## Suggested Agent Prompt

```text
Implement subbundle 06 only. Extract CRM/HR helper logic page by page, preserve sensitive-data and navigation behavior, run targeted CRM/HR tests, and record page-level proof in the workbook and execution report.
```
