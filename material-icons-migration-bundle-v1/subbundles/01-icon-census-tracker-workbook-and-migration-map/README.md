# Icon Census, Tracker Workbook, And Migration Map

## Status

- `Ready`

## Objective

- Lock the inventory, workbook, CSV exports, hot spots, and token map so later phases execute from a trusted scope instead of ad hoc search output.

## Covered Inputs

- `N001` Use the bundle workflow and gate the work before implementation.
- `N004` Map all places where `Icon.razor` or pure icons are used.
- `N005` First identify all places and make an Excel tracker for done versus remaining work.

## Prerequisites

- `none`

## Exact Source References

- `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/inputs/00-original-request.md`
- `C:/repositories/CanDoItAll/output/spreadsheet/material-icons-migration-tracker.xlsx`
- `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/inventories/01-material-icon-inventory.csv`
- `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/inventories/02-material-icon-token-map.csv`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Identity/Icon.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Web/Components/App.razor`

## Deliverables

- Workbook and CSV exports with the current scope, token list, status columns, and planned subbundle ownership.
- `inventories/01-scope-inventory.md` with counts, hot spots, exclusions, and execution notes.
- Updated bundle documentation that turns the raw request into execution-ready subbundles.

## Dependency Impact

- Every downstream subbundle depends on the workbook being complete enough to prevent hidden icon systems or forgotten raw glyph surfaces.
- Weak proof here invalidates later route-level claims because downstream work would be based on incomplete scope.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Scan the repo for shared renderers, `<Icon>` call sites, token assignments, raw glyph markup, CSS coupling, and external icon assets.
2. Generate the workbook and CSV exports.
3. Summarize the counts, hot spots, exclusions, and dirty worktree risks in the bundle inventory files.
4. Confirm the later subbundles reference the workbook and the right source clusters.

## Scope Exceptions

- No source exception is allowed here; missing scope must reopen this subbundle.

## Do Not Do

- Do not start product code edits in this phase.
- Do not collapse shorthand tokens into `fix later` buckets.
- Do not ignore the already modified Workbench files when assigning later work.

## Acceptance Checklist

- Workbook exists at the tracked output path.
- Inventory CSV and token map CSV exist under the bundle.
- The bundle inventory summary reflects the workbook counts and top hotspots.
- Later subbundles can point back to a stable scope artifact instead of rediscovering files.

## Proof Required

- File existence proof for the workbook and both CSV exports.
- Bundle docs updated to cite the workbook and current census counts.
- Prepared-stage bundle validator passes after the bundle-writing work is complete.

## Browser Validation Logging

- `N/A - this census phase does not change browser-visible behavior.`

## Progression Gate

- Do not start subbundle `02` until the workbook, CSV exports, scope summary, and dirty-worktree notes are all present and trusted.

## Suggested Agent Prompt

```text
Implement only subbundle 01. Generate or refresh the icon census workbook and CSV exports, update the scope inventory, and do not make product code changes.
```
