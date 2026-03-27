# Sub-Bundle 2: Shared Floating Window Host Checklist

- [ ] extract prompt factory floating window behavior into a shared `ComponentKit` host or equivalent shared canvas window contract
- [ ] support drag with handle-based initiation
- [ ] support resize
- [ ] support minimize
- [ ] support normalize back to default geometry
- [ ] support hide and toolbar-driven restore
- [ ] clamp default positions below the toolbar safe zone
- [ ] clamp dragged positions so windows cannot cover the toolbar
- [ ] persist window geometry and visibility in the structure page UI state
- [ ] verify selection and health windows use the shared host instead of page-specific duplicate code

Acceptance result:

- structure canvas windows behave like real movable workbench surfaces and respect toolbar safety
