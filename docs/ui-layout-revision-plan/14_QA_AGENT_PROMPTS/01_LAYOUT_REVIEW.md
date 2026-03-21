# QA Prompt: Layout Review

Review the phase-1 implementation for layout quality and user-flow clarity.

## Focus Areas

- duplicate page introduction
- shell chrome noise
- action hierarchy
- selected-state clarity in list/detail pages
- form sectioning
- sticky action behavior
- responsive behavior
- route-level filter completeness on migrated list/detail pages
- missing top-level create actions on admin/settings-style pages

## Pages To Review

- Dashboard
- Projects
- Resources
- Prompt Gallery
- Validation Center
- Test Lab
- Activity
- Automation
- Settings
- Project Calendar

## Questions To Answer

1. Is the user's main task obvious quickly?
2. Is the primary action placed where a user would expect it?
3. Do similar pages now share the same structure?
4. Is any page still visibly assembled from one-off layout decisions?
5. Does the shell help the page, or still compete with it?
6. Did the route keep the filters and quick actions required for real task throughput, not just the new visual shell?

## Required Output

- explicit findings with route/component references
- residual risks if no hard findings exist
- note any responsive problems separately
