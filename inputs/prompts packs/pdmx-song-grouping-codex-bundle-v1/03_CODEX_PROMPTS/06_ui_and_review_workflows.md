# Prompt 06 — UI And Review Workflows

## Objective

Add the grouping UI needed for browsing, review, and correction.

## Tasks

1. Update dashboard maintenance actions for grouping stages.
2. Update catalog filters and row badges.
3. Add grouping panel to score detail.
4. Expand groups page and group detail page.
5. Add a dry-run review surface or equivalent workflow.
6. Surface evidence, confidence, and lock state.

## Boundaries

- preserve current workstation layout/style
- avoid giant pages by extracting grouping components
- do not hide critical evidence behind impossible navigation

## Required tests

- catalog shows group badges
- score detail shows memberships and evidence
- group detail shows member list and diagnostics
- dry-run review route loads
- at least one manual edit UI smoke test

## Review checklist

- [ ] user can see more than one group membership
- [ ] user can understand why a song was grouped
- [ ] user can correct grouping without raw DB edits
- [ ] ambiguous clusters are visible somewhere in UI
