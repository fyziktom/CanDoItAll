# Scope Inventory

## Production Surfaces

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
  Toolbar markup, button placement, and user-facing feedback.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs`
  Best candidate seam for command orchestration and status messaging.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
  Current persistence seam for structure surfaces and node coordinates.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructurePlacementPolicy.cs`
  Adjacent placement logic that must remain create-focused and not absorb subtree recomposition.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\wwwroot\js\workbenchInterop.js`
  Current source of shape bounds used by the renderer. Needed for parity if the C# engine uses the same bounds.

## Test Surfaces

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
  Toolbar and page orchestration coverage.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePlacementPolicyTests.cs`
  Adjacent placement-policy coverage that should remain green after the new engine is added elsewhere.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`
  Service persistence and hierarchy projection coverage.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
  Existing browser-proof home for project structure UI validation.

## Explicit Non-Goals

- No link reconnection workflow changes.
- No replacement of the existing create-time placement policy.
- No automatic re-layout on load or sync.
