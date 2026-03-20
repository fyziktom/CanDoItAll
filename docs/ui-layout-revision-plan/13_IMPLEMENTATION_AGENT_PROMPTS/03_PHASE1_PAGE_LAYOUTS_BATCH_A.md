# Prompt: Phase 1 Page Layouts Batch A

You are migrating the highest-value standard pages first.

## Scope

- Dashboard
- Projects
- Resources

## Read First

- `../06_PAGE_BY_PAGE_REVIEW.md`
- `../08_LAYOUT_PATTERNS_AND_ASCII_SKETCHES.md`
- `../09_RECOMMENDED_DESIGN_RULES.md`

## Goals

1. Make the dashboard actionable instead of purely explanatory.
2. Convert Projects to a stronger list/detail + wizard composition.
3. Convert Resources to a stronger list/detail + structured editor composition.

## Requirements

- use the new shared page-composition components
- move primary actions to the page-header level where appropriate
- add selected-row state to list panes
- add better empty-state treatment
- add sticky action treatment for long editors where appropriate

## Do Not Touch

- protected workbench internals
- deeper service or data-model behavior unless required for layout correctness

## Expected Outputs

- migrated `Home.razor`
- migrated `ProjectsPage.razor`
- migrated `ResourcesPage.razor`
- any small component refinements necessary to support those migrations

## Self-Check Before Finishing

- Is the dashboard now a true resume/start surface?
- Do Projects and Resources look like members of the same product family?
- Are primary actions now easier to find?
- Can a user tell which item is selected in each list?

