# Current State

## Active CanvasLib Asset Pipeline

- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json` defines `styles`, `runtimeScripts`, `previewScripts`, and `calendarScripts`.
- `C:\repositories\CanDoItAll\tools\canvaslib\build-assets.cjs` currently copies each listed source file directly into a single public output file and regenerates:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibHeadAssets.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibBodyAssets.razor`
- The pipeline already supports multiple files per asset category. That means the refactor can split source and public output without introducing a bundler.

## Current CanvasLib Hotspots

- The current CanvasLib files above the user’s hard closure limit are:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\canvasWorkbenchInterop.js` at 7692 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js` at 7692 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css-src\workbench\canvas-workbench.css` at 3747 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css` at 3747 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\calendar\zy-canvas-calendar.js` at 3464 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\zy-canvas-calendar.js` at 3464 lines
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor` is large at 687 lines but below the user’s hard stop. The current request is specifically about CanvasLib resource files in `wwwroot`, so the hard closure gate applies there first.

## Current Folder Shape

- CanvasLib already separates source trees at the top level:
  - `wwwroot\js-src\runtime`
  - `wwwroot\js-src\preview`
  - `wwwroot\js-src\calendar`
  - `wwwroot\js-src\services`
  - `wwwroot\css-src\workbench`
- The problem is that the largest runtime, calendar, and stylesheet assets are still monolithic leaf files. The structure is only one level deep, so the hard-to-review code still lives in giant single files.

## Duplicate Asset Situation

- The three `canvasWorkbenchInterop.js` copies currently present are:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\wwwroot\js\canvasWorkbenchInterop.js`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\canvasWorkbenchInterop.js`
- The CanvasLib pair is a source file plus its generated public output. The extra duplicate is the legacy `ComponentKit` publish copy.
- Source-only search across `C:\repositories\CanDoItAll\src` and `C:\repositories\CanDoItAll\tests` did not find active consumers that reference `_content/CanDoItAll.ComponentKit/...` assets.
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\CanDoItAll.ComponentKit.csproj` still implicitly publishes its `wwwroot` content, so the duplicate static-web-asset surface still exists even if nothing actively references it.

## Reusable Planning Material

- `C:\repositories\CanDoItAll\project-structure-canvas-execution-bundle-v3\05_CANVASLIB_REORGANIZATION_PLAN.md` already proposes the right directional structure:
  - deeper `wwwroot\js-src\workbench\{shared,state,render,interaction,overlays,export,runtime}`
  - deeper `wwwroot\css-src\workbench\*`
  - generated public outputs instead of hand-maintained monoliths
- `C:\repositories\CanDoItAll\project-structure-canvas-execution-bundle-v3\06_FILE_SPLIT_PLAN.md` also provides a credible file-budget blueprint that aligns with the user’s maintainability goal.
