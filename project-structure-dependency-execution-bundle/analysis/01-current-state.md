# Current State

## Existing Domain And Persistence

- `C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\ProjectObjectContracts.cs` already defines `ProjectObjectLinkKind.DependsOn`, so the dependency feature should extend existing link semantics rather than invent a new relationship type.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs` already persists user-authored project links through `ProjectObjectLinkRecord` and exposes them in `ProjectStructureLink`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchService.cs` already loads dependency links into the workbench surface and supports creating links through `LinkObjectsAsync`, but it does not expose an unlink or delete-link API and does not carry an explicit duration-seconds field on nodes.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureChecklistService.cs` already interprets `DependsOn` and `Blocks`, so dependency direction has existing downstream meaning that must stay consistent.

## Existing UI And Canvas Behavior

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` already has a hidden connect flow via `linkModeSourceId`, but it is driven by context actions and does not expose the architect-requested toolbar tools or preview affordances.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolbarActions.razor` is the current top-toolbar insertion point and can host the new tool cluster.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js` already renders curved links and arrowheads, but hover and delete hit zones for links are not currently registered.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js` handles node selection and dragging, but does not yet provide a dedicated delete mode or dependency-preview completion workflow.

## Existing Summary, Export, And Agent Surfaces

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureSummaryModels.cs` builds summary rows mainly from hierarchy and date fields, not from dependency topology.
- The existing Mermaid Gantt export uses synthetic scheduling when dates are absent, but it is not dependency-aware and has no explicit duration-seconds source.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAgentContracts.cs` and `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAgentService.cs` expose node and link data, but there is no dedicated dependency-readiness surface for agent consumers yet.

## Existing Validation Harness

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs` already exercises link persistence and node deletion, making it a good foundation for deeper dependency tests.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs` can validate page and tool-state changes.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PlaywrightAppFixture.cs` and `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs` already use a managed fresh SQLite profile, matching the request to avoid the legacy database.
