# Scope Inventory

## Duplicate Inventory

| Area | Current duplicate pattern | Evidence | Planned handling |
| --- | --- | --- | --- |
| CanvasLib `wwwroot` | `css` vs `css-src` and `js` vs `js-src` identical mirror trees | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot`, `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json` | Remove the redundant tree and make tooling operate on one canonical asset copy |
| Legacy canvas project | `CanDoItAll.ComponentKit` duplicates CanvasLib canvas components and graph classes | `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit`, `dotnet sln C:\repositories\CanDoItAll\CanDoItAll.slnx list` | Verify unused, then retire or explicitly carve out as legacy if hidden usage appears |

## CanvasLib Flat Folder Hotspots

| Folder | Current direct file count | Problem |
| --- | --- | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components` | `40` | Flat component root obscures feature grouping and makes discovery harder |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Graph` | `29` | Backing classes for overlays, primitives, and interaction are mixed together |

## In-Scope Large Files

| File | Lines | Planned handling |
| --- | --- | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\CanvasWorkbenchContracts.cs` | `495` | Split into coherent workbench model and event contract files |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor` | `802` | Review during topology work; split only if the folder reorganization exposes an obvious nearby decomposition worth taking |

## Repo-Wide Hotspots Recorded But Not Owned Here

| File or folder | Evidence | Reason not owned by this bundle |
| --- | --- | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor` | `3311` lines | Outside CanvasLib stabilization unless a required consumer change forces nearby edits |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs` | `2361` lines | Outside the request scope |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\wwwroot\css\output.css` | `2059` lines | Generated stylesheet in another library; not part of the requested CanvasLib cleanup |
