# Current State

The bundle is now implemented and revalidated against the live repository state.

- The active workbench scene is canvas-based for frames, links, nodes, and minimap in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js-src\runtime\canvasWorkbenchInterop.js`.
- Export now composes the frame, link, and node canvases directly instead of cloning DOM into SVG `foreignObject`.
- ProjectStructure persists canvas view state with delayed write-behind and patches committed move deltas without unconditional reload.
- PromptFactory uses the shared CanvasWorkbench with delayed write-behind for drag and state-change persistence.
- CanvasLib assets are centralized through generated include components consumed by both the web shell and the sandbox shell.
- Full validation is green:
  `npm run canvaslib:verify-assets`
  `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --nologo`
  `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --nologo`
