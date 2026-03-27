# Current State

## Confirmed Owners

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
  - owns the blocks explorer floating-window markup, toolbox copy, search box, and section rendering
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ToolWindows.cs`
  - owns the toolbox window state, accordion expansion state, and search filtering
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
  - overrides the toolbox window chrome and owns the page-specific toolbox layout tuning
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasFloatingWindow.razor`
  - already owns the shared minimize, normalize, hide, and drag handle chrome that the feedback asks to reuse
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
  - owns the shared floating-window styling and the base dark toolbox skin
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvas-floating-window.js`
  - owns floating-window drag, resize, and safe-zone clamping behavior

## Verified Findings

- The blocks explorer currently opts out of the shared window chrome by setting `ShowHeader="false"` in `ProjectStructurePage.razor`, so the standard minimize and hide actions are not rendered.
- `ProjectStructurePage.razor.css` intentionally strips the toolbox window shell down to a transparent container, which also suppresses the shared floating-window chrome even though the dark toolbox body already exists.
- The accordion state is handled in `ProjectStructurePage.ToolWindows.cs` through `expandedToolboxGroupKey`, but the browser proof currently focuses on search scrolling rather than explicit section-open behavior.
- The toolbox search results already use a dedicated scroll container, but the current browser coverage does not assert that labels remain readable after scrolling or that the standard header still leaves enough visible space.
- The Prompt Factory toolbox already demonstrates the standard floating-window pattern with shared header controls and drag behavior, so there is a nearby in-repo example to follow.

## Existing Test Surface

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanvasFloatingWindowTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PromptLibraryVerificationTests.cs`
