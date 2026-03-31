# prefix-stabilization-and-compatibility-shims

## Status

- `Ready`

## Objective

- Canonicalize the legacy non-canvas shared wrapper prefix on `cad-*`, add temporary compatibility shims where needed, and stop expanding raw `zy-*` usage on changed shared surfaces.

## Covered Inputs

- `N06`
- `R07`, `R08`

## Prerequisites

- Subbundles `01` through `05` completed and trusted

## Exact Source References

- `C:\repositories\CanDoItAll\Tailwind\controls\buttons.css`
- `C:\repositories\CanDoItAll\Tailwind\layout\sheets.css`
- `C:\repositories\CanDoItAll\Tailwind\layout\stats.css`
- `C:\repositories\CanDoItAll\Tailwind\forms\tag-editor.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\tabs.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\Button.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor`
- `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\inventories\02-prefix-summary.md`
- `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\inventories\01-prefix-inventory.csv`

## Deliverables

- Canonical `cad-*` selectors or emitted classes on changed legacy shared non-canvas wrapper surfaces
- Compatibility aliases for risky legacy `zy-*` callers
- Updated prefix inventory showing the changed direction

## Dependency Impact

- Final closure depends on this phase because the request explicitly called out prefix stabilization as part of the style-system work.
- Existing `cda-*` semantic tone selectors remain stable in this bundle because they already encode the shared BaseLib theme vocabulary the request explicitly referenced.

## Validation Depth

- `UI and compatibility-proof`

## Implementation Steps

1. Rename changed shared selectors to `cad-*` canonical names.
2. Add alias selectors or dual emitted classes for risky legacy `zy-*` callers where needed.
3. Update changed Razor markup to the canonical prefix.
4. Re-run the prefix inventory and record the migration trend.

## Scope Exceptions

- Canvas-only `zy-*` selectors are still excluded from this immediate bundle.

## Do Not Do

- Do not do a repo-wide blind rename.
- Do not remove aliases until the changed routes are proven stable.

## Acceptance Checklist

- Changed legacy shared wrapper surfaces now expose `cad-*` as the canonical class path.
- Compatibility aliases exist wherever a hard cut would break changed surfaces.
- No changed legacy shared wrapper surface introduced during this phase relies on `zy-*` as its only class path.

## Proof Required

- Updated prefix inventory or summary notes
- Route screenshots proving alias-backed changes did not regress behavior

## Browser Validation Logging

- Target routes: whichever migrated routes still exercise the renamed shared selectors
- Viewports: `1600x1000` plus narrower-width pass where applicable
- Required actions: navigate, inspect renamed shared surfaces, and verify the route still renders without missing shared styling
- Evidence paths: use the same route screenshots captured during subbundle `05` plus any additional prefix-specific proof
- Review questions: Did the canonical rename land without breaking the shared surface, and are aliases clearly transitional instead of becoming permanent clutter?

## Progression Gate

- The changed shared selectors must be canonicalized on `cad-*`, aliases must preserve behavior, and no route proof may show missing shared styling caused by the rename.

## Suggested Agent Prompt

```text
Implement this subbundle only. Canonicalize changed shared non-canvas selectors to `cad-*`, keep aliases where needed for safety, and prove the rename did not break the migrated routes.
```
