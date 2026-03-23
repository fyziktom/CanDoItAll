# Prompt: Phase 1 Validation And Fixes

You are doing the final stabilization pass after the layout implementation.

## Scope

- consistency fixes
- responsive fixes
- shared-component compliance fixes
- test-driven regression fixes

## Read First

- `../11_RISKS_AND_GUARDRAILS.md`
- `../12_VALIDATION_CHECKLIST.md`
- `../15_CHECKLISTS/GLOBAL_UI_CHECKLIST.md`
- `../15_CHECKLISTS/QA_SIGNOFF_CHECKLIST.md`

## Required Validation

- relevant component tests
- protected-route component tests
- Playwright smoke/regression tests named in `03_PHASE1_PROTECTED_AREAS.md`

## Goals

1. Remove remaining page-to-page inconsistencies.
2. Fix responsive regressions.
3. Fix any shell-mode issues.
4. Keep protected areas stable.

## Expected Outputs

- final consistency fixes
- final responsive fixes
- updated tests if visible labels changed intentionally
- clear summary of remaining risk if any

## Self-Check Before Finishing

- Are standard pages now visibly part of one system?
- Is navigation available and usable below `lg`?
- Did protected workbench behavior remain intact?
- Are there any remaining one-off page-local layout hacks that should have become shared components?

