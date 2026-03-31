# Scope Inventory

## Inventory Artifacts

- Workbook: `C:/repositories/CanDoItAll/output/spreadsheet/material-icons-migration-tracker.xlsx`
- Inventory CSV: `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/inventories/01-material-icon-inventory.csv`
- Token map CSV: `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/inventories/02-material-icon-token-map.csv`

## Census Totals

- `239` tracked inventory rows are currently visible in the workbook.
- `91` unique literal tokens are currently visible in the token map.
- Category split: `160` token sources, `39` Font Awesome renderer rows, `32` raw icon markup rows, `16` CSS coupling rows, `13` `<Icon>` call sites, and `1` external runtime asset reference.

## Top Project Hotspots

- `CanDoItAll.Modules.Workbench`: `75` rows
- `CanDoItAll.Modules.Factory`: `70` rows
- `CanDoItAll.Components.CanvasLib`: `31` rows
- `CanDoItAll.Components`: `27` rows
- `CanDoItAll.Components.BaseLib`: `23` rows
- `CanDoItAll.Components.Sandbox`: `10` rows
- `CanDoItAll.Web`: `3` rows

## Initial Subbundle Allocation

- `02 Local Material Icons foundation and shared renderer conversion`: `40` rows
- `03 BaseLib and legacy shared-component icon migration`: `16` rows
- `04 Non-canvas app and module icon adoption`: `82` rows
- `05 Workbench, canvas, and closure validation`: `101` rows

## Representative High-Risk Files

- `C:/repositories/CanDoItAll/src/CanDoItAll.Web/Components/App.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Identity/Icon.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Buttons/Button.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/AppShell.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/AppTabStrip.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Factory/Pages/Components/PromptFactoryHistoryToolbar.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor`

## Token Hotspots

- Highest-frequency literal tokens in the token map today include `QA`, `flow`, `fork`, `clear`, `open`, `!`, `BR`, `IN`, `SET`, `skip`, `summary`, `use`, and `x`.
- The token map distinguishes named tokens, raw glyphs, Font Awesome literals, shorthand badges, and dynamic expressions so mapping decisions stay explicit.

## Exclusions And Derived Outputs

- `obj/` and `bin/` trees are excluded from the workbook because they are generated.
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css` is treated as a derived output that should be regenerated from source changes rather than edited directly.

## Execution Note

- During implementation, update the workbook `Migration Status`, `Proposed Material Icon`, and `Validation Notes` columns instead of tracking progress only in chat.
