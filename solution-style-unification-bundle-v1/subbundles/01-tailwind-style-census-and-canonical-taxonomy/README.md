# Tailwind style census and canonical taxonomy

## Status

- `Blocked`

## Objective

- Produce the non-canvas style census workbook, lock the explicit canvas exclusion list, define the canonical repeated-family taxonomy, and record the baseline progress metrics that every downstream phase will use.

## Covered Inputs

- `REQ-05`, `REQ-06`, `REQ-07`, `REQ-14`, `REQ-16`
- Raw prompt step `1`
- Raw prompt exclusion of CanvasLib and canvas-adjacent drawing surfaces

## Prerequisites

- Bundle readiness gate passed.
- No earlier execution subbundles.

## Exact Source References

- `C:\repositories\CanDoItAll\Tailwind\input.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ProjectStructureAgentSettingsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor.css`
- `C:\repositories\CanDoItAll\output\spreadsheet\style-census-initial.xlsx`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectModalHost.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectHierarchyModal.razor`

## Deliverables

- A refreshed Excel workbook covering non-canvas Tailwind-like raw HTML elements, exact duplicates, normalized duplicates, similar families, and excluded canvas scope.
- A written taxonomy of canonical shared families and unification rules.
- A documented exclusion list for all canvas and canvas-host files that this wave must not edit.
- Baseline metrics for repeated occurrences, custom CSS hotspots, and top migration targets.
- A refreshed component-level hotspot sheet that separates top non-canvas derived Razor components from explicitly deferred canvas-adjacent files.

## Dependency Impact

- Every downstream subbundle depends on this census being complete enough to choose the right abstractions.
- If the exclusion boundary is wrong here, later edits could violate the explicit CanvasLib constraint.
- If the canonical family list is weak here, subbundles `02`, `03`, and `04` will duplicate or miss shared styles.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Refresh the workbook from the current repo state outside the excluded canvas scope.
2. Review the highest-frequency exact patterns, normalized groups, and family signatures.
3. Record the canonical shared families and the first-wave unification rules.
4. Record the baseline metrics that later subbundles must improve against.
5. Update the bundle inventory files and execution report rows for this foundation.

## Scope Exceptions

- CanvasLib and canvas-host surfaces remain excluded by design in this wave.

## Do Not Do

- Do not refactor page markup yet.
- Do not edit BaseLib or Tailwind architecture yet beyond what is necessary to generate the census artifacts.
- Do not silently ignore outlier families; document them.

## Acceptance Checklist

- The workbook exists and opens as a valid `.xlsx`.
- The workbook groups exact duplicates and similar families by occurrence count.
- The exclusion list is explicit and matches the user’s canvas constraint.
- The taxonomy and baseline metrics are recorded in bundle inventories.

## Proof Required

- Workbook path recorded in `output/spreadsheet`.
- Inventory markdown updated with counts and top hotspots.
- No browser proof required for this analytical phase.

## Browser Validation Logging

- `N/A`. This subbundle is analytical and produces the census workbook, exclusion list, and metrics baseline.

## Progression Gate

- The workbook, taxonomy, exclusion list, and baseline metrics exist and are recorded.
- No downstream subbundle may start while the inventory still has unclassified high-frequency families.

## Suggested Agent Prompt

```text
Refresh the non-canvas style census, update the workbook and inventory markdown, confirm the exact canvas exclusion list, and record the baseline metrics needed by the later Tailwind and BaseLib refactors. Do not refactor UI markup in this subbundle.
```
