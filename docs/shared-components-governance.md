# Shared Components Governance

## Ownership

- `CanDoItAll.Components.Common`, `CanDoItAll.Components.BaseLib`, and `CanDoItAll.Components.CanvasLib` are owned from the CanDoItAll repo.
- Zyphonote or any other downstream repo must request shared-library changes through the `Requests` folders in the owning library instead of editing shared code from the consumer repo.

## Runtime Boundaries

- `CanDoItAll.Components.Sandbox` is the only approved home for preview, demo, tuning, and fake-data component assets.
- Runtime libraries must not keep catalog-only components, preview cards, or tuning boundaries.
- App-specific styling must stay in the app-specific repo or app-specific library. Shared libraries must not depend on `zyphonote-compat.css` or any other app-global stylesheet.
