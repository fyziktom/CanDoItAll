# Normalized Requirements

## R01 Shared Theme Contract

- Create one shared non-canvas theme contract for BaseLib-driven UI with semantic tokens for common surfaces, text, borders, focus styling, and action/status tones.

## R02 Tailwind Source Of Truth

- Define the shared contract through the Tailwind-owned styling pipeline, not through ad hoc app CSS files or duplicated page-level color utilities.

## R03 Consumer Override

- Make the shipped theme overridable by downstream apps consuming `CanDoItAll.Components.BaseLib` as a NuGet package without requiring them to rebuild BaseLib’s Tailwind sources.

## R04 Runtime Switching

- Provide a runtime-usable theme host or scope so a rendered UI can switch between at least a default light theme and a simple dark theme during the same session.

## R05 BaseLib Adoption

- Move core BaseLib primitives away from hard-coded palette utilities and onto the shared semantic theme contract, including shared radii where those primitives currently encode their own rounding rules.

## R06 Route Migration

- Replace high-value route and module hotspots that still hard-code palette utilities when those surfaces should instead depend on the shared BaseLib theme contract.

## R07 Prefix Stabilization

- Stabilize non-canvas shared style naming around `cad-*` and remove new reliance on `zy-*` prefixes for shared BaseLib/Tailwind surfaces.

## R08 Compatibility Safety

- Use compatibility aliases or staged migration where needed so the prefix and theme refactor does not break existing dependent routes during the same bundle.

## R09 Inventory And Architecture Review

- Produce lists and Excel-style inventories before implementation, then create and critically QA-review the architecture before coding the feature phases.

## R10 Zyphonote Reuse Confirmation

- Confirm, without implementing Zyphonote changes yet, that the resulting contract is reusable from future Zyphonote server and WebAssembly apps once those apps adopt BaseLib-centered surfaces.
