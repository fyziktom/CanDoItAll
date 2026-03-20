# UI Layout Revision Plan

This folder is a planning-only handoff package for a future implementation agent and a future QA/review agent.

It is based on the current repository state, especially:

- `src/CanDoItAll.Web`
- `src/CanDoItAll.ComponentKit`
- `src/CanDoItAll.Components`
- `src/CanDoItAll.Modules.*`
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- existing planning docs under `docs/ui-shared-components` and `docs/canvases-improvements`

No production UI changes are made by this package.

## What This Package Covers

- current shell and layout review
- probable user stories and user intent by screen
- global UX and information-architecture findings
- page-by-page recommendations tied to real routes and Razor components
- shared component-library gaps
- phase-1 protected-area rules for the two stable canvas workbenches
- ASCII layout proposals for repeated page archetypes
- implementation sequencing, guardrails, QA prompts, and operational checklists

## What This Package Does Not Do

- it does not implement the UI plan
- it does not redesign the two stable canvas workbenches internally
- it does not propose a full visual rebrand
- it does not rewrite module/service architecture

## Recommended Reading Order For The Implementation Agent

1. `01_EXECUTIVE_SUMMARY.md`
2. `02_SCOPE_AND_CONSTRAINTS.md`
3. `03_PHASE1_PROTECTED_AREAS.md`
4. `05_UI_REVIEW_GLOBAL_FINDINGS.md`
5. `06_PAGE_BY_PAGE_REVIEW.md`
6. `07_COMPONENT_LIBRARY_GAP_ANALYSIS.md`
7. `08_LAYOUT_PATTERNS_AND_ASCII_SKETCHES.md`
8. `09_RECOMMENDED_DESIGN_RULES.md`
9. `10_IMPLEMENTATION_STRATEGY.md`
10. `11_RISKS_AND_GUARDRAILS.md`
11. `12_VALIDATION_CHECKLIST.md`
12. `13_IMPLEMENTATION_AGENT_PROMPTS/00_START_HERE.md`
13. the remaining implementation prompts in order
14. `15_CHECKLISTS/IMPLEMENTATION_HANDOFF_CHECKLIST.md`

## Recommended Reading Order For The QA Agent

1. `01_EXECUTIVE_SUMMARY.md`
2. `03_PHASE1_PROTECTED_AREAS.md`
3. `05_UI_REVIEW_GLOBAL_FINDINGS.md`
4. `06_PAGE_BY_PAGE_REVIEW.md`
5. `09_RECOMMENDED_DESIGN_RULES.md`
6. `11_RISKS_AND_GUARDRAILS.md`
7. `12_VALIDATION_CHECKLIST.md`
8. `14_QA_AGENT_PROMPTS/00_QA_START_HERE.md`
9. the remaining QA prompts in order
10. `15_CHECKLISTS/QA_SIGNOFF_CHECKLIST.md`

## Fast Context

The application already has:

- a reusable shell concept
- a workbench tab system
- a newer `ComponentKit` layer
- thin low-level components in `CanDoItAll.Components`
- two mature canvas-heavy work surfaces

The problem is not a total absence of shared UI. The problem is that the shared layers stop too early, so most pages still improvise the last mile of layout, action placement, state presentation, and form composition.

That is why phase 1 should focus on layout foundations, page archetypes, and reusable composition patterns instead of ad hoc page cleanup.

