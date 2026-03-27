# 08 Zyphonote Components Split And Adoption

## Objective

Create `Zyphonote.Components`, move Zyphonote-only components into it, and rewire Zyphonote apps to use `BaseLib` and `CanvasLib` for shared concerns.

## Exact Source References

Current Zyphonote component sources:

- `C:\repositories\Zyphonote\src\App.Components`
- `C:\repositories\Zyphonote\src\App.Blazor\Components`
- `C:\repositories\Zyphonote\src\App.Blazor\Pages`
- `C:\repositories\Zyphonote\src\App.AI.TranscriptionLab`
- `C:\repositories\Zyphonote\src\App.PdmxTool`
- `C:\repositories\Zyphonote\src\App.Blazor\_Imports.razor`
- `C:\repositories\Zyphonote\src\App.Server\Components\App.razor`
- `C:\repositories\Zyphonote\src\App.Blazor\Services\PlanningCalendarExportService.cs`
- `C:\repositories\Zyphonote\src\App.Blazor\Services\PlanningWorkspaceService.cs`

Classification input:

- `..\..\inventories\02-componentkit-and-app-component-classification.md`
- `..\..\inventories\04-cross-repo-dependency-map.md`

## Implementation Steps

1. Create `Zyphonote.Components`.
2. Move Zyphonote-only components into it according to the classification inventory.
3. Replace shared wrapper usage with `CanDoItAll.Components.BaseLib`.
4. Replace shared canvas usage with `CanDoItAll.Components.CanvasLib`.
5. Keep app-specific CSS and JS in Zyphonote.
6. Use temporary compatibility wrappers only if they reduce page churn safely.
7. Remove direct dependency on the old `App.Components` wrapper project when the migration wave is complete.
8. Update server asset includes:
   - remove old shared asset paths
   - add new shared library asset paths
   - remove CDN icons after local shared assets are working

## Hard Rules

- do not edit shared library source from inside the Zyphonote repo
- do not copy app-global CSS into shared libraries
- do not force every Zyphonote component into shared ownership
- do not break the current canvas-driven planning pages while changing namespaces

## Acceptance Checklist

- Zyphonote builds against `BaseLib` and `CanvasLib`
- `Zyphonote.Components` owns the remaining app-specific component layer
- old wrapper ownership is removed or clearly staged for removal
- app-specific CSS and JS remain local to Zyphonote
- major account/planning/canvas flows still render correctly

## Proof Required

- project reference diff for Zyphonote
- build output
- screenshot proof for key account, marketplace, and planning pages
- note listing any temporary compatibility wrappers left in place

## Suggested Agent Prompt

```text
Implement subbundle 08 only.

Create Zyphonote.Components and move Zyphonote-only UI into it while rewiring shared wrapper and canvas usage to the new CanDoItAll-owned BaseLib and CanvasLib. Keep the migration conservative: do not over-share branded or domain-specific components, and do not edit the shared libraries from the Zyphonote side.
```
