# Prompt: Phase 1 Page Layouts Batch B

You are migrating the high-friction review and admin pages.

## Scope

- Validation Center
- Test Lab
- Settings

## Read First

- `../06_PAGE_BY_PAGE_REVIEW.md`
- `../08_LAYOUT_PATTERNS_AND_ASCII_SKETCHES.md`
- `../09_RECOMMENDED_DESIGN_RULES.md`

## Goals

1. Standardize list/detail behavior.
2. Break long forms into readable sections.
3. Make run/save/reset/delete actions predictable.
4. Separate unrelated admin concerns in Settings.

## Requirements

- add selected-row state
- add list headers and filters where called for
- use form sections instead of one uninterrupted field stack
- use sticky action regions on long editors
- use local tabs or section navigation in Settings if needed

## Do Not Touch

- protected workbench internals
- route semantics unless necessary for the new layout

## Expected Outputs

- migrated `ValidationCenterPage.razor`
- migrated `TestLabPage.razor`
- migrated `SettingsPage.razor`

## Self-Check Before Finishing

- Are findings and review decisions more visible on Validation?
- Is Test Lab easier to scan section-by-section?
- Are Workspace, Secrets, and Providers easier to understand as separate jobs?

