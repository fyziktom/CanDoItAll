# Workbench, Canvas, And Closure Validation

## Status

- `Ready`

## Objective

- Finish the icon migration on the Workbench and canvas-heavy surfaces, merge safely around the locally modified files, and then close the bundle with route proof, workbook updates, and raw-note closure.

## Covered Inputs

- `N004` Map all places where `Icon.razor` or pure icons are used and replace them.
- `N005` Keep the tracker accurate about what is done and what still needs change.

## Prerequisites

- `subbundles/01-icon-census-tracker-workbook-and-migration-map` completed and trusted.
- `subbundles/02-local-material-icons-foundation-and-shared-renderer-conversion` completed and trusted.
- `subbundles/03-baselib-and-legacy-shared-component-icon-migration` completed and trusted.
- `subbundles/04-non-canvas-app-and-module-icon-adoption` completed and trusted.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Graph/Composition/NodeCardComposer.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Graph/Overlays/ContextMenuHost.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Graph/Primitives/ChipBadgePrimitive.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureToolboxWindow.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Navigation/TreeViewNodeRow.razor`

## Deliverables

- Workbench and canvas surfaces migrated to Material icon output with explicit token mappings for shorthand and preview cases.
- Merge-safe updates in the already modified files without clobbering unrelated local changes.
- Final workbook status updates, raw-note closure, and browser analytics recorded in the execution report.

## Dependency Impact

- This is the final route-heavy closure phase; if it is weak, the bundle cannot honestly claim solution-wide completion.
- Later auditing depends on the execution report and workbook being fully updated here.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Review the remaining workbook rows assigned to Workbench and canvas surfaces, including the dirty files.
2. Replace remaining Workbench and canvas raw glyphs, token previews, and `<Icon>` call sites with Material icon equivalents or explicit mappings.
3. Update CSS and layout hooks that still expect old icon classes or raw glyph wrappers.
4. Capture Workbench and canvas browser proof, then close raw notes and update the workbook and execution report.

## Scope Exceptions

- If a shorthand badge cannot be mapped cleanly without user input, record it explicitly and keep the row open instead of guessing silently.

## Do Not Do

- Do not overwrite unrelated local changes in the already modified Workbench and BaseLib files.
- Do not hide unresolved Workbench or canvas tokens in a residual-risk paragraph while marking the bundle complete.
- Do not close the bundle without updating the workbook and raw-note closure table.

## Acceptance Checklist

- Remaining Workbench and canvas icon rows are either completed or honestly blocked in the workbook.
- The already modified files are merged safely and still contain the user’s unrelated local work.
- The execution report includes populated gate rows, browser analytics rows, and raw-note closure rows.
- The final browser pass shows no leftover Font Awesome output or raw glyph icon escapes on the targeted Workbench surfaces.

## Proof Required

- `dotnet build C:/repositories/CanDoItAll/CanDoItAll.slnx`
- Browser proof on `/projects/{ProjectId:guid}/structure` and `/groups/canvas`
- If needed, additional proof on `/prompt-factory` when Workbench-adjacent token behavior is affected
- Desktop and narrower-width screenshots showing the changed Workbench and canvas states
- Updated workbook and populated execution report tables

## Browser Validation Logging

- Route: `/projects/{ProjectId:guid}/structure`, `/groups/canvas`, and `/prompt-factory` if token-driven canvas behavior changes there
- Viewports: `1600x900` first pass, then `768x1024`
- Actions: navigate, open the Workbench and relevant dialogs or menus, inspect icon-heavy states, and capture screenshots for open states rather than closed triggers only
- Screenshots: record the actual file paths in `reviews/01-execution-report.md`
- Review questions: confirm treeview chevrons, Workbench actions, canvas preview tokens, and open overlays all render the intended Material icons without clipping or confusion

## Progression Gate

- The bundle can only close after the workbook shows the remaining scope honestly, the execution report is populated, the raw notes are closed, and the final validator passes.

## Suggested Agent Prompt

```text
Implement only subbundle 05. Finish the Workbench and canvas icon migration, merge safely around the already modified files, update the workbook and execution report, and do not close the bundle until the raw notes and final browser proof are complete.
```
