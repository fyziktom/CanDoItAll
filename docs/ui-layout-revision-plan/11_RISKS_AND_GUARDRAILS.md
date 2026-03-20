# Risks And Guardrails

## 1. Risk: Over-Refactoring The Shell

Problem:

- `MainLayout.razor` is already large and stateful
- aggressive shell rewrites can destabilize navigation, tab persistence, or tuning mode

Guardrails:

- introduce shell modes incrementally
- keep route detection explicit and testable
- preserve current workbench tab contracts

## 2. Risk: Breaking Stable Canvas Workbenches

Problem:

- the two workbench pages are large, coupled, and already protected by UI tests

Guardrails:

- no phase-1 behavioral edits inside protected workbench internals
- shell-mode changes only
- rerun the component and Playwright tests listed in `03_PHASE1_PROTECTED_AREAS.md`

## 3. Risk: One-Off Fixes Recreate The Same Inconsistency

Problem:

- page-by-page cleanup without shared primitives will simply move the inconsistency around

Guardrails:

- add shared composition components before broad migration
- reject page-local wrappers that duplicate `ComponentKit` responsibilities

## 4. Risk: Mobile / Tablet Regression

Problem:

- navigation is already incomplete below `lg`
- a shell refactor can accidentally preserve desktop assumptions

Guardrails:

- make responsive navigation a phase-1 shell requirement
- review `md` and `lg` breakpoints deliberately
- do not treat stacked content as a complete responsive strategy by itself

## 5. Risk: Component Explosion Across Two Libraries

Problem:

- the repo already has two UI libraries with different maturity levels

Guardrails:

- place page-composition additions in `CanDoItAll.ComponentKit`
- do not add overlapping page-composition components to `CanDoItAll.Components`
- document ownership clearly in implementation PR notes

## 6. Risk: Hidden Coupling In Query-String Detail Pages

Problem:

- several pages load editor state from query-string-selected records
- list-detail standardization can accidentally break selection and deep-link behavior

Guardrails:

- preserve query parameter semantics
- keep selected-state visuals separate from navigation mechanics
- test direct links where applicable

## 7. Risk: Regressing Workbench-Oriented Context

Problem:

- reducing shell chrome too aggressively can remove context power users currently rely on

Guardrails:

- demote context, do not delete it blindly
- compress or relocate secondary context before removing it
- use route-aware shell behavior rather than a one-size-fits-all shell

## 8. Risk: Test Fragility During Copy Or Heading Changes

Problem:

- existing tests use text selectors and route expectations

Guardrails:

- avoid changing route semantics in phase 1
- if visible labels must change, update tests in the same implementation batch
- keep protected-route test coverage green before merging layout work

## 9. Risk: Poor Action Hierarchy After Migration

Problem:

- adding more shared components can still leave pages with multiple "primary" buttons

Guardrails:

- enforce one-primary-action-per-scope in review
- verify page header, row actions, and sticky footer are not competing

## 10. Risk: Tuning / Development Utilities Becoming Unreachable

Problem:

- shell cleanup may hide the development-oriented tuning panel in unsafe ways

Guardrails:

- if the global right rail is hidden on some routes, provide a deliberate alternate access path in development mode
- do not silently remove tuning access from development builds

## 11. Risk: Replacing Too Much At Once

Problem:

- broad simultaneous migration will make debugging layout regressions harder

Guardrails:

- follow the page batches in `10_IMPLEMENTATION_STRATEGY.md`
- merge after stable route groups, not after a repo-wide restyle
