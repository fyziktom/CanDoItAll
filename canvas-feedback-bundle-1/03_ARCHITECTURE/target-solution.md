# Target solution

## Architecture decision

Apply the changes at the shared canvas layer first, then add the smallest page-local override for the project structure toolbox.

## Shared changes

1. Extend the icon catalog so the requested icon tokens resolve through the existing `Icon` component.
2. Replace floating window text buttons with icon-only controls while keeping `aria-label` and `title` attributes explicit.
3. Refactor the create composer into:
   - an overview block
   - a wizard-style step rail
   - a scrollable section container
   - a persistent action row

## Page-local changes

1. Override the project structure toolbox body to a single-column grid.
2. Ensure the toolbox root and body use `minmax(0, 1fr)` patterns so the inner sections actually scroll.
3. Tighten section and item density slightly to better match a toolbox/explorer feel.

## Risk management

- Avoid a hard multi-page wizard because that would force widespread Playwright rewrites.
- Preserve field selectors and field ordering so current automation stays valid.
