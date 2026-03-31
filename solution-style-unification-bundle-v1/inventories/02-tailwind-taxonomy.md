# Tailwind Taxonomy

## Canonical Shared Families

- `Buttons`
- Canonicalize dark primary actions, neutral secondary actions, muted active-state actions, destructive outlined actions, and inline icon actions.
- `Forms`
- Canonicalize field labels, input shells, field rows, section stacks, and helper/meta text.
- `Typography`
- Canonicalize eyebrow/meta text, section titles, supporting body text, and compact note text.
- `Layout`
- Canonicalize vertical rhythm stacks, two-column form grids, neutral surface cards, toolbar rows, and list/detail split shells.
- `Feedback`
- Canonicalize callouts, empty states, notification surfaces, and help affordance chrome.
- `Navigation`
- Canonicalize page headers, toolbar actions, tabs, and navigation badges.

## Unification Rules

- Keep one canonical border-radius per family unless behavior requires a distinct variant.
- Collapse padding differences that do not materially change touch target or density.
- Prefer one text tracking and casing treatment for eyebrow/meta text instead of several near-identical values.
- Prefer one neutral white surface card family instead of page-specific copies.
- When a repeated family already maps cleanly to a BaseLib primitive, migrate markup to that primitive instead of creating a second semantic class.
