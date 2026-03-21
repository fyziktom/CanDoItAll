# UI Layout Revision Plan

This folder started as a planning-only handoff package. It now also records the real implementation/QA status of the phase-1 UI revision.

It is based on the current repository state, especially:

- `src/CanDoItAll.Web`
- `src/CanDoItAll.ComponentKit`
- `src/CanDoItAll.Components`
- `src/CanDoItAll.Modules.*`
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- existing planning docs under `docs/ui-shared-components` and `docs/canvases-improvements`

## Current Status Update (2026-03-20)

The following phase-1 work is implemented in the product:

- shared shell mode split between standard pages and focus workbenches
- shared page composition primitives in `CanDoItAll.ComponentKit`
- standard-page migrations for dashboard, projects, resources, validation, test lab, settings, prompt gallery, activity, automation, and project calendar
- focus-shell adoption for project structure and prompt factory

The following QA findings changed the practical rollout status:

- the implementation can look partially missing if `src/CanDoItAll.Components/wwwroot/css/output.css` is stale
- final UI QA must rebuild Tailwind output before accepting screenshots as valid
- resources, test lab, prompt gallery, and settings still needed several route-level finishing actions after the first migration pass
- protected structure-canvas regressions were found in shared interop during live validation and had to be fixed before signoff
- a follow-up protected-route QA pass found that the desktop main menu had dropped from focus-workbench routes while docked, which is not acceptable for structure or prompt-factory pages
- maximize evidence is only valid when the workbench host anchors to the live viewport at `(0,0)` and covers the actual app view instead of expanding inside an inner card
- radial-menu density also needed a second polish pass: larger icon/text use inside the hexes, no duplicate text in the numeric priority submenu, and a much lower zoom floor for large maps

Live review evidence now exists in `output/playwright/` and should be treated as part of the handoff context.

## What This Package Covers

- current shell and layout review
- probable user stories and user intent by screen
- global UX and information-architecture findings
- page-by-page recommendations tied to real routes and Razor components
- shared component-library gaps
- phase-1 protected-area rules for the two stable canvas workbenches
- ASCII layout proposals for repeated page archetypes
- implementation sequencing, guardrails, QA prompts, and operational checklists
- implementation/QA status notes discovered during the live phase-1 rollout

## What This Package Does Not Do

- it does not redesign the two stable canvas workbenches internally
- it does not propose a full visual rebrand
- it does not rewrite module/service architecture

The plan documents still should not be treated as the source of truth for code. They are a guide plus a QA/status record.

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
