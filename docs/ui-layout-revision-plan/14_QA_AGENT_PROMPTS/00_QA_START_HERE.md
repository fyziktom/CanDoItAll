# QA Start Here

You are reviewing a completed phase-1 UI layout implementation.

## Read First

1. `../01_EXECUTIVE_SUMMARY.md`
2. `../03_PHASE1_PROTECTED_AREAS.md`
3. `../05_UI_REVIEW_GLOBAL_FINDINGS.md`
4. `../06_PAGE_BY_PAGE_REVIEW.md`
5. `../09_RECOMMENDED_DESIGN_RULES.md`
6. `../11_RISKS_AND_GUARDRAILS.md`
7. `../12_VALIDATION_CHECKLIST.md`

## Review Order

1. `01_LAYOUT_REVIEW.md`
2. `02_COMPONENT_CONSISTENCY_REVIEW.md`
3. `03_PROTECTED_AREA_REGRESSION_REVIEW.md`

## Review Priorities

1. find layout regressions and hierarchy failures first
2. find component consistency gaps second
3. verify protected canvas behavior third

## Required Runtime Rules

- run the app from a live runtime before accepting screenshots
- rebuild Tailwind output before visual signoff
- do not accept a screenshot as valid evidence if the page is still using stale generated CSS

## Required Output Style

- findings first, ordered by severity
- include route/component references when possible
- call out missing tests or unverified risks explicitly
