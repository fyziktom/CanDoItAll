# Current State

## Asset Topology

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot` currently contains four sibling trees:
  - `css`
  - `css-src`
  - `js`
  - `js-src`
- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json` points to `css-src\**` and `js-src\**` sources and mirrors them into identical `css\**` and `js\**` outputs.
- `C:\repositories\CanDoItAll\tools\canvaslib\build-assets.cjs` and `verify-assets.cjs` currently enforce that both copies stay identical.
- Repo-wide sibling `*-src` duplication was scanned and only CanvasLib `wwwroot` matched this pattern.

## CanvasLib Folder Density

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components` currently contains 40 direct child `.razor` files plus `Shared`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Graph` currently contains 29 direct child `.cs` files in one flat folder.
- This flat structure mirrors the earlier asset monolith split, but the C# side was not reorganized alongside it.

## Large File Hotspots In Scope

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\CanvasWorkbenchContracts.cs` is currently 495 lines and mixes:
  - surface models
  - node models
  - UI-state serialization
  - chrome or action contracts
  - event records
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor` is currently 802 lines.
- CanvasLib no longer has any file above 2000 lines, but the request is broader than the 2000-line gate. The remaining problem is folder density and mixed-responsibility files.

## Repo-Level Duplicate Surface Finding

- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit` still contains a parallel canvas API surface:
  - duplicate `Canvas\Graph` files with the same names as CanvasLib
  - duplicate canvas-related `.razor` components with the same names as CanvasLib
- `dotnet sln C:\repositories\CanDoItAll\CanDoItAll.slnx list` does not include `CanDoItAll.ComponentKit`.
- No source or project references outside `CanDoItAll.ComponentKit` itself were found during the preparation scan.
- This makes `ComponentKit` a legacy duplicate candidate, but retirement must still be validated before deletion.

## Out-Of-Scope Hotspots Recorded For Follow-Up

- Repo-wide large files outside the requested CanvasLib scope still exist:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor` at 3311 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs` at 2361 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\wwwroot\css\output.css` at 2059 lines
- These are not part of this bundle unless the implementation work uncovers a direct dependency that forces nearby changes.
