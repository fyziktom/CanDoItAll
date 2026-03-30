# Structured Input

## Request Type

- Structural refactor and asset-pipeline reorganization for `CanDoItAll.Components.CanvasLib`

## Raw Constraints

- Use `candoitall-bundle-workflow`.
- Keep only the `canvasWorkbenchInterop.js` copy that belongs to CanvasLib as the active shipped copy.
- Split `canvasWorkbenchInterop.js` into logical parts.
- Split CanvasLib `wwwroot` JS and CSS resources into folders so the package is easier to manage.
- Validate the result as a senior QA C# / Blazor engineer.
- The task is not complete while any CanvasLib file exceeds 2000 lines.

## Repo Facts Confirmed During Preparation

- CanvasLib currently has three source hotspots above 2000 lines:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\canvasWorkbenchInterop.js` at 7692 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css-src\workbench\canvas-workbench.css` at 3747 lines
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\calendar\zy-canvas-calendar.js` at 3464 lines
- Their generated public outputs in `wwwroot\js` and `wwwroot\` are also above 2000 lines.
- The CanvasLib asset build pipeline copies source files one-to-one from `wwwroot\js-src` and `wwwroot\css-src` into public `wwwroot` outputs and regenerates include components.
- `CanDoItAll.ComponentKit` still publishes a duplicate `wwwroot` asset set, including `canvasWorkbenchInterop.js`, `zy-canvas-calendar.js`, and `canvas-workbench.css`.
- Source-only audit across `src\` and `tests\` found no active `_content/CanDoItAll.ComponentKit/...` consumer references that would justify keeping the duplicate asset publish path active.

## Assumptions

- The public runtime may change from a single public `canvasWorkbenchInterop.js` URL to an ordered list of smaller CanvasLib runtime assets, as long as CanvasLib consumers continue to load correctly through `CanvasLibBodyAssets.razor`.
- Preview assets do not need structural changes unless touched by manifest or include-order cleanup.
- ComponentKit can remain as a legacy project, but it should stop shipping the duplicate CanvasLib static asset set unless a build or test proves that assumption false.

## Validation Expectations

- Rebuild CanvasLib generated assets.
- Verify the generated asset manifest and include components.
- Build and run targeted tests covering CanvasLib consumers.
- Perform browser truth for structure and calendar routes after asset reorder and split.
- Run a final line-count audit for `src\CanDoItAll.Components.CanvasLib` with a hard closure gate of no file above 2000 lines.
