# Current State

## Current Icon Delivery

- `C:/repositories/CanDoItAll/src/CanDoItAll.Web/Components/App.razor` currently links to the remote Font Awesome CDN stylesheet.
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Identity/Icon.razor` and `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/Icon.razor` both resolve icon tokens through `FontAwesomeIconCatalog` and render `<i class="rz-fa-icon ...">`.
- Shared components such as `Button`, `Tabs`, and `Steps` in both BaseLib and the legacy `CanDoItAll.Components` project duplicate the same Font Awesome render path instead of delegating through one shared renderer.

## Census Snapshot

- The generated workbook at `C:/repositories/CanDoItAll/output/spreadsheet/material-icons-migration-tracker.xlsx` currently contains `239` inventory rows and `91` unique literal tokens.
- Category counts from the CSV census: `160` token-assignment rows, `39` Font Awesome renderer rows, `32` raw icon markup rows, `16` CSS coupling rows, `13` `<Icon>` call-site rows, and `1` external asset row.
- Top affected projects are `CanDoItAll.Modules.Workbench` (`75` rows), `CanDoItAll.Modules.Factory` (`70` rows), `CanDoItAll.Components.CanvasLib` (`31` rows), `CanDoItAll.Components` (`27` rows), `CanDoItAll.Components.BaseLib` (`23` rows), and `CanDoItAll.Components.Sandbox` (`10` rows).
- High-frequency literal tokens currently needing mapping include `QA`, `flow`, `fork`, `clear`, `open`, `!`, `BR`, `IN`, `SET`, `skip`, `summary`, `use`, and raw text icons like `x`.

## Major Hotspots

- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Identity/Icon.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Identity/FontAwesomeIconCatalog.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Buttons/Button.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Navigation/Tabs.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Navigation/Steps.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/AppShell.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/AppTabStrip.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Factory/Pages/Components/PromptFactoryHistoryToolbar.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor`

## Dirty Worktree Considerations

- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Navigation/TreeViewNodeRow.razor` already has local modifications and also contains icon logic.
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureToolboxWindow.razor` already has local modifications and participates in the icon-heavy Workbench surface.
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` and `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css` already have local modifications and contain CSS coupled to the current icon classes.

## Derived Or Excluded Outputs

- `obj/` and `bin/` directories were excluded from the workbook census because they are generated build output.
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css` was treated as a derived asset; the migration should target the source components and source CSS files that regenerate it.
