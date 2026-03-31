# Target Solution

## Shared Asset Layer

- Host the Material Icons font and stylesheet inside the solution, preferably under `CanDoItAll.Components.BaseLib` static web assets, so downstream projects consume the assets through normal `_content` resolution.
- Source the local asset files from the Google Material Icons stylesheet requested by the user, but ship the resulting files from the repository rather than from Google at runtime.

## Shared Render Path

- Convert both shared `Icon.razor` implementations to render Material Icons markup instead of `<i class="rz-fa-icon ...">`.
- Consolidate duplicated shared render logic in `Button`, `Tabs`, and `Steps` around the same Material icon output conventions so there is one recognizable styling model.
- Favor a shared icon class contract that works for both direct component rendering and CSS layout hooks without keeping the old Font Awesome class names alive.

## Token Compatibility Policy

- Preserve the existing semantic token idea, but map legacy tokens through a Material-oriented alias catalog instead of a Font Awesome catalog.
- Review literal Font Awesome values such as `fa-angle-right` and shorthand badges such as `QA`, `PF`, `BR`, `!`, `x`, and `zZ` explicitly rather than assuming they can be passed through untouched.
- Update the token workbook as mappings are decided so remaining ambiguity stays visible.

## CSS And Layout Policy

- Update source CSS files that currently target Font Awesome or raw glyph wrappers to target the new shared Material icon class contract.
- Regenerate derived CSS outputs only after the source components and source styles are corrected.
- Keep icon spacing, centering, and line-height rules component-specific where needed, but avoid recreating parallel icon systems per route.

## Tracking And Proof Artifacts

- Treat `C:/repositories/CanDoItAll/output/spreadsheet/material-icons-migration-tracker.xlsx` as the execution ledger for scope, status, and token mapping decisions.
- Keep `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/inventories/01-material-icon-inventory.csv` and `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/inventories/02-material-icon-token-map.csv` synchronized as lightweight text exports of the same scope.
- Record browser analytics and subbundle gate outcomes in `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/reviews/01-execution-report.md`.
