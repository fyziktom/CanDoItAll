# Scope Inventory

## Primary Inventory Artifacts

- Workbook: `C:\repositories\CanDoItAll\output\spreadsheet\theme-system-scope-inventory.xlsx`
- File hotspots: `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\inventories\04-file-hotspots.csv`
- Prefix inventory: `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\inventories\01-prefix-inventory.csv`
- Color hotspots: `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\inventories\02-color-hotspots.csv`
- Routes and theme hooks: `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\inventories\03-route-and-theme-hooks.csv`
- Execution-step list: `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\inventories\05-execution-steps.csv`

## In Scope

- Tailwind shared non-canvas style files under `C:\repositories\CanDoItAll\Tailwind`
- BaseLib non-canvas primitives and their shared CSS
- BaseLib-consuming web/module routes that still hard-code palette utilities
- Shared prefix stabilization for non-canvas BaseLib/Tailwind surfaces
- Browser-visible runtime light/dark theme proof
- Documentation confirming the reuse path for Zyphonote apps

## Related But Secondary

- Legacy wrapper components under `C:\repositories\CanDoItAll\src\CanDoItAll.Components`
- Sandbox demo surfaces used for focused proof
- Existing broad style-unification bundle used as context only

## Explicitly Excluded From Immediate Implementation

- CanvasLib’s full `zy-*` footprint and canvas-only theme system migration
- Zyphonote server and WebAssembly app refactors
- Any string-based public style API shortening
