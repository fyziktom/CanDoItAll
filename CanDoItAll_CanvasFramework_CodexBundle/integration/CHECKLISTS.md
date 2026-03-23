# Checklists

## Implementation checklist

- [ ] Use existing shared framework entry points first.
- [ ] Extract page-local logic into adapters or shared components.
- [ ] Update relevant tests.
- [ ] Remove or deprecate old duplicate paths.
- [ ] Keep JS modules narrow and purpose-specific.

## Architecture checklist

- [ ] Low-level primitive vs high-level component boundary is clear.
- [ ] Shared vs domain-specific ownership is clear.
- [ ] No business-heavy logic drift into JS.
- [ ] Persistence/state schema is typed and versionable.

## UX/UI checklist

- [ ] Selection, hover, focus, and menu behavior remain coherent.
- [ ] Empty/loading/error states are handled intentionally.
- [ ] Truncation, images, badges, and overlays follow shared patterns.
- [ ] Keyboard alternatives exist for critical interactions.

## Performance checklist

- [ ] No unnecessary full-surface rebuild in hot interaction loops.
- [ ] Measurement, connector geometry, or event layout work is cached or batched appropriately.
- [ ] Layer count remains deliberate.
- [ ] Large-scene fallback strategy is documented when relevant.

## Validation checklist

- [ ] Per-component validation prompt was used.
- [ ] Wave-level validation prompt was used.
- [ ] Future-feature simulation still passes for affected areas.
