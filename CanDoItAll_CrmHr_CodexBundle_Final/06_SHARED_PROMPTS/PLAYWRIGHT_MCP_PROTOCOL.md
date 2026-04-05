# Playwright MCP protocol

## Primary validation path

Use a Playwright-capable browser automation path for every UI-changing CRM/HR bundle.

Preferred order:

1. **Playwright MCP** if available in the execution environment
2. Repository Playwright test project under `tests/CanDoItAll.Tests.Playwright`
3. Manual browser fallback only if the first two are impossible, and only with equivalent screenshot evidence

## Required behavior

For each UI bundle, the browser automation must at minimum:

1. open the relevant CRM/HR route
2. create or edit representative records
3. reload the page to confirm persistence
4. navigate to linked routes where the bundle requires cross-module visibility
5. capture screenshots
6. record a semantic review note explaining what is visible and why it satisfies the bundle

## Screenshot storage convention

Store screenshots under a stable folder such as:

```text
output/playwright/crm-hr/<bundle-id>/
```

Suggested filenames:

- `01_initial.png`
- `02_created-record.png`
- `03_filtered-view.png`
- `04_detail-panel.png`
- `05_reload-persisted.png`
- `06_cross-module-proof.png`

## Semantic review requirement

Every screenshot set must be accompanied by plain-language notes that answer:

- which route is shown,
- which entity or flow is visible,
- whether key text is readable,
- whether primary actions are present,
- whether expected relationship data appears,
- whether any visual defect is visible.

## What to inspect in screenshots

- clipped titles or labels
- broken list/detail proportions
- buttons hidden below fold without scroll affordance
- incorrect status badges
- missing secondary tabs
- wrong route after save/convert actions
- duplicate rows after merge or conversion
- visible `#blazor-error-ui`
- privacy leaks on sensitive sections

## Cross-module Playwright expectations

At minimum, the final suite should include flows for:

- `/crm-hr`
- `/crm-hr/directory`
- `/crm-hr/crm`
- `/crm-hr/workforce`
- `/crm-hr/recruiting`
- `/crm-hr/agents`
- `/crm-hr/assignments`
- `/projects`
- `/projects/{id}/structure`

## Important note

A screenshot file by itself is **not evidence**.  
Evidence is: browser action + screenshot + semantic review note + passing acceptance criteria.
