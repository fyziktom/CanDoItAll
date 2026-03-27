# Current State

## Confirmed Owners

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor`
  - owns the help and settings overlays rendered inside the shared canvas stage
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
  - owns help overlay layout, floating window chrome, node palette gradients, and shared canvas layering
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.RichDefinitions.cs`
  - owns typed create definitions such as `add-file-markdown`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
  - owns the project structure page, inspector preview, modal preview markup, and the `CanvasWorkbench` overlay slot usage
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
  - owns preview dialog/backdrop styling

## Verified Findings

- The help overlay currently uses `.cw-help-overlay` with `place-items: start center`, so it anchors near the top instead of centering vertically.
- The markdown create action definition currently does not require a file, so the create dialog has no upload/drop zone for that flow.
- File nodes already resolve subtype-specific palettes in `ProjectStructureGraphAdapter`, but the shared palette gradients are still subtle and do not strongly match the requested file-type cues.
- The preview dialog markup for file, summary, and Mermaid dialogs lives outside the `CanvasWorkbench` overlay slot, so the previews are not guaranteed to belong to the canvas shell in maximized mode.

## Existing Test Surface

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanvasWorkbenchTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureGraphAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
