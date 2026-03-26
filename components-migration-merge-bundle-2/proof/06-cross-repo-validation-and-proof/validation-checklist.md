# Validation Checklist

## Builds

- [ ] `CanDoItAll.slnx` builds cleanly
- [ ] `Zyphonote.slnx` builds cleanly

## Shared Ownership

- [ ] `BaseLib` components are grouped into family subfolders
- [ ] no new shared component is left flat in `BaseLib\Components`
- [ ] family-local enums and support types are no longer dumped into a global junk-drawer file
- [ ] `Zyphonote.Components.csproj` uses explicit ownership, not wildcard linkage

## Consumer Migration

- [ ] `UiButton`, `UiCard`, `UiField`, and `UiSection` are removed or clearly temporary
- [ ] thin Zyphonote wrappers were retired where shared primitives already cover the behavior
- [ ] score-workbench layout wrappers are not pretending to be shared library assets anymore

## Styling

- [ ] `BaseLib` uses shared Tailwind styling or small family-scoped CSS
- [ ] Zyphonote page-specific refinements remain local
- [ ] `zyphonote-compat.css` was not imported into shared ownership

## Visual Surfaces

- [ ] marketplace views
- [ ] my scores views
- [ ] seller profile views
- [ ] legal pages
- [ ] login and register or auth flows
- [ ] workspace and modal flows
