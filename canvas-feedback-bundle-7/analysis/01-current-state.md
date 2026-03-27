# Current State

## Repo And Product Surface

- The shared project-structure canvas is built from `Modules.Workbench` data and rendered through `CanDoItAll.Components.CanvasLib`.
- The current worktree started clean before bundle preparation.
- The feedback targets the project-structure page rather than a standalone demo, so changes must remain compatible with the existing Workbench runtime and command flow.

## Path And Lead-Text Ownership

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs` currently derives user-facing facts such as `Path`, `Project`, `URL`, and `Work dir`.
- The same descriptor currently collapses the first two facts into plain `LeadText`, which means long paths spill directly into the node card with no typed representation of what the value means.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs` maps the descriptor output into generic canvas node contracts. Today that mapping carries only plain strings for title, subtitle, and lead text.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js` renders `leadText` as plain node text, so it has no dedicated affordance for compact path display, tooltip intent, or clipboard feedback.

## Double-Click Ownership

- Shared canvas interop already routes a node double-click back into .NET through `OnNodeOpened`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` currently handles node open by calling `ExecuteCommandAsync(ProjectStructureCommandKind.Open, nodeId)`.
- For preview-capable nodes, the page opens the preview panel. For non-preview nodes, the current flow executes the existing open command immediately.
- The page already owns inspector-action resolution and command execution support through:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeEditing.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.RuntimeLaunch.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- That makes the page the correct place to decide whether a double-click should preview, edit, launch PowerShell, open a wizard, or expose an explicit unsupported state.

## Settings Surface Ownership

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor` currently renders the toolbar settings button with the literal text `cfg`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css` styles the settings card but does not yet reserve an explicit toolbar-safe top offset for the opened overlay.
- The screenshot extracted from feedback confirms the current modal can render too high and visually disappear behind the toolbar band.

## Validation Baseline

- Existing test coverage already includes project-structure component and Playwright test projects:
  - `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
  - `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- No existing focused proof was found for compact path copying, non-preview quick-action modal composition, or toolbar-safe settings placement, so bundle execution must add targeted coverage.
