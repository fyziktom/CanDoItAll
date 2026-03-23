# Start Here

Use this prompt sequence to implement phase 1 safely.

## Read Before Coding

1. `../01_EXECUTIVE_SUMMARY.md`
2. `../02_SCOPE_AND_CONSTRAINTS.md`
3. `../03_PHASE1_PROTECTED_AREAS.md`
4. `../05_UI_REVIEW_GLOBAL_FINDINGS.md`
5. `../06_PAGE_BY_PAGE_REVIEW.md`
6. `../07_COMPONENT_LIBRARY_GAP_ANALYSIS.md`
7. `../08_LAYOUT_PATTERNS_AND_ASCII_SKETCHES.md`
8. `../09_RECOMMENDED_DESIGN_RULES.md`
9. `../10_IMPLEMENTATION_STRATEGY.md`
10. `../11_RISKS_AND_GUARDRAILS.md`
11. `../12_VALIDATION_CHECKLIST.md`

## Required Execution Order

1. `01_PHASE1_GLOBAL_FOUNDATIONS.md`
2. `02_PHASE1_COMPONENT_GAPS.md`
3. `03_PHASE1_PAGE_LAYOUTS_BATCH_A.md`
4. `04_PHASE1_PAGE_LAYOUTS_BATCH_B.md`
5. `05_PHASE1_PAGE_LAYOUTS_BATCH_C.md`
6. `06_PHASE1_PROTECTED_ROUTE_SHELLS.md`
7. `07_PHASE1_VALIDATION_AND_FIXES.md`

## Non-Negotiables

- do not redesign canvas internals
- do not refactor `CanvasWorkbench` behavior
- do not rewrite prompt/session or project-graph logic to make layout changes easier
- do not create a third UI layer outside `CanDoItAll.ComponentKit` unless absolutely necessary
- prefer incremental route migration over repo-wide restyling

## Required Outcome

At the end of phase 1 the application should have:

- quieter, more deliberate shell behavior
- standardized management-page composition
- predictable primary actions
- clearer list/detail selection
- better long-form structure
- protected workbench routes framed by a focus shell instead of noisy chrome

## Required Reporting In Each Implementation Step

- files changed
- what shared pattern was added or reused
- which pages were migrated
- what was intentionally not touched
- what was tested

