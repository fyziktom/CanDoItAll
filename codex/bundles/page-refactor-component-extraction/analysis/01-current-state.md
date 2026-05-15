# Current State

## Inventory Method

- Scanned `*.razor` files under `src` and identified routable pages by `@page`.
- Measured total lines, approximate markup lines, approximate `@code` lines, and method-like declarations.
- Reviewed the heaviest route pages and page-owned components for helper and component extraction candidates.
- Searched tests for existing component and Playwright coverage tied to the affected surfaces.

## Highest-Risk Route Pages

| Route | File | Lines | Code lines | Methods | Primary refactor signal |
| --- | --- | ---: | ---: | ---: | --- |
| `/prompt-factory` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor` | 3030 | 1226 | 63 | canvas graph helpers, state persistence, large page shell |
| `/projects/{ProjectId:guid}/structure` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` | 2748 | 2049 | 75 | node helper logic, attachment preview helpers, canvas/window orchestration |
| `/plugins` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor` | 1345 | 761 | 32 | busy-key helpers, connection editor state, render fragments |
| `/crm-hr/directory` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrDirectoryPage.razor` | 1276 | 655 | 22 | nested editor view model, filters, clone/build helpers |
| `/crm-hr/crm` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrCrmPage.razor` | 1268 | 752 | 37 | filters, editor factories, opportunity state helpers |
| `/crm-hr/workforce` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrWorkforcePage.razor` | 992 | 534 | 18 | filters, formatting helpers, quick-create model |
| `/agents/workflows` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor` | 890 | 0 | 0 | large markup shell already delegated to components |
| `/scheduler` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\Pages\SchedulerPlannerPage.razor` | 757 | 326 | 16 | page-level scheduling handlers and render regions |
| `/projects` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor` | 714 | 540 | 38 | list orchestration, modal/board callbacks |
| `/prompt-gallery` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor` | 567 | 268 | 9 | nested catalog classes and grouping helpers |

## Long Page-Owned Components

| Component | Lines | Code lines | Primary refactor signal |
| --- | ---: | ---: | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor` | 1660 | 0 | very large markup-only editor surface |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor` | 1613 | 912 | provider/storage helpers mixed with panel rendering |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor` | 1291 | 286 | many dialog regions inside one component |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureProcessAssignmentDialog.razor` | 919 | 470 | process assignment rendering plus selection/filter helpers |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\StorageSettingsPanel.razor` | 636 | 309 | storage helper logic mixed with settings UI |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectModalHost.razor` | 634 | 119 | large modal host shell |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor` | 603 | 202 | board rendering plus callbacks |

## Existing Test Coverage Signals

- Project structure has dense component tests under `tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`, `ProjectStructurePageSimpleMutationTests.cs`, `ProjectStructurePageMoveTests.cs`, `ProjectStructurePartyPickerTests.cs`, and database switch tests.
- Prompt factory has component tests under `tests\CanDoItAll.Tests.Components\PromptFactoryPageTests.cs` and Playwright tests under `tests\CanDoItAll.Tests.Playwright\PromptFactoryBrowserTests.cs`.
- Plugins has component tests under `tests\CanDoItAll.Tests.Components\PluginsPageTests.cs`.
- CRM/HR has Playwright flows under `tests\CanDoItAll.Tests.Playwright\CrmHr*Tests.cs` and component tests for navigation and privacy boundaries.
- Settings data source coverage exists under `tests\CanDoItAll.Tests.Components\SettingsPageDataSourcesTests.cs`.

## Component Guidance Status

- The CanDoItAll components MCP was queried during preparation for BaseLib layout guidance.
- The MCP failed with `Transport closed`.
- Execution must retry the MCP before introducing new structural layout markup; if it remains unavailable, inspect local BaseLib component files and usage examples with `rg` and document the fallback in the execution report.
