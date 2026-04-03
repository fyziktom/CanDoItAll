# Validation prompt

Validate **B04 — CRM accounts, contacts, stakeholders, interaction journal, and follow-ups**.

## Required validation layers

- `tests/CanDoItAll.Tests.Components/CrmPageTests.cs`
- `tests/CanDoItAll.Tests.Integration/CrmInteractionIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Playwright/CrmInteractionFlowTests.cs`

## Browser routes to validate

- `/crm-hr/crm`
- `/crm-hr/directory`

## Validation checklist

1. Confirm acceptance criteria in `ACCEPTANCE_CRITERIA.md`.
2. Run targeted automated tests for the changed area.
3. Open the listed routes and execute the key user flows.
4. Capture the required screenshots.
5. Write a semantic review note explaining what the screenshots prove.
6. Check for regressions in related modules mentioned in `FILE_REFERENCES.md`.

## Special attention points

- no visible `#blazor-error-ui`
- no clipped text or broken layouts
- no duplicate records after save/merge/convert flows
- persistence survives reload
- cross-module visibility appears where this bundle requires it
