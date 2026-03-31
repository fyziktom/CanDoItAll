# Target Solution

## Intended End State

- Shared non-canvas styling lives primarily in Tailwind component-layer imports under `Tailwind/`, not in one monolithic `input.css` block and not in repeated raw markup strings across pages.
- BaseLib is the first-class reusable styling surface for buttons, labels, fields, cards, layout shells, headers, badges, lists, and feedback. Pages should consume those primitives or semantic shared classes instead of recreating them.
- Page-scoped CSS remains only where behavior is truly page-specific or host-specific. It should no longer hold generic button, label, card, spacing, or shell styling.

## Tailwind Layer Shape

- `Tailwind/input.css` remains the single entry point.
- `Tailwind/input.css` imports responsibility-based files in a stable order.
- `Tailwind/foundation/*.css` holds global resets and Radzen bridge helpers.
- `Tailwind/layout/*.css` holds shell, stack, grid, and surface scaffolds.
- `Tailwind/controls/*.css` holds buttons, chips, pills, badges, and inline actions.
- `Tailwind/forms/*.css` holds labels, fields, text inputs, tag editors, switches, and form rows.
- `Tailwind/navigation/*.css` holds headers, toolbars, tabs, and list-panel navigation surfaces.
- `Tailwind/feedback/*.css` holds alerts, callouts, notifications, help affordances, and empty states.
- `Tailwind/compatibility/*.css` holds transitional or legacy shared classes that still have broad consumers during the migration.

## Shared Class Strategy

- Preserve existing broadly consumed semantic classes such as `zy-sheet-*`, `zy-tag-textedit*`, and `zy-stat-card*` when renaming would add churn without value.
- Add new semantic shared classes only for real repeated families found in the census, not for one-off page fragments.
- Canonical family definitions should collapse near-duplicate patterns such as dark primary action buttons, white secondary buttons, eyebrow/meta text, field labels, surface cards, and vertical spacing shells.
- Raw utility strings remain acceptable for genuine one-off layout nudges, but repeated combinations graduate into shared classes or BaseLib components.

## BaseLib Integration Strategy

- Prefer expanding existing BaseLib primitives over introducing new wrapper components with almost no behavior.
- Introduce a new BaseLib primitive only when the same non-canvas pattern appears across multiple modules and cannot be expressed clearly with the current component library.
- Keep component APIs strongly typed through enums, parameter objects, or existing primitive types instead of stringly typed “variant” parameters.

## Migration Boundaries

- Migrate non-canvas modules only after the shared Tailwind layer and BaseLib foundations are proven.
- Leave CanvasLib and canvas-host surfaces untouched in this wave.
- Keep visual system continuity with the current app. This is a maintainability refactor, not a broad art-direction rewrite.
