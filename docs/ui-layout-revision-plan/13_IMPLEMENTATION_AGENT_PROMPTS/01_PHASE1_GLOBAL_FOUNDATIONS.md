# Prompt: Phase 1 Global Foundations

You are implementing only the global shell and layout foundations for phase 1.

## Scope

- `MainLayout.razor`
- route-aware shell behavior
- global navigation accessibility on smaller breakpoints
- global right-rail behavior
- top-level duplication between shell intro and page intro
- tab-strip framing only where it affects shell composition

## Read First

- `../03_PHASE1_PROTECTED_AREAS.md`
- `../05_UI_REVIEW_GLOBAL_FINDINGS.md`
- `../08_LAYOUT_PATTERNS_AND_ASCII_SKETCHES.md`
- `../09_RECOMMENDED_DESIGN_RULES.md`

## Goals

1. Introduce explicit shell modes:
   - `StandardPage`
   - `FocusWorkbench`
2. Remove or reduce duplicate route introduction.
3. Make primary navigation reachable below `lg`.
4. Make the shell right rail route-aware instead of always-on.
5. Keep workbench tabs and route context intact.

## Do Not Touch

- canvas internals
- workbench JS interop
- prompt build logic
- project-structure command logic

## Expected Outputs

- shell-mode routing logic
- a compact route-context treatment that does not duplicate page headers
- a mobile/tablet navigation fallback
- a right-rail visibility policy
- no page-specific CRUD migration yet

## Self-Check Before Finishing

- Can every route still be reached below `lg`?
- Is there now only one major page introduction on standard routes?
- Are protected routes eligible for a quieter shell mode without changing their internals?
- Did you avoid deep page-level migrations in this step?

