# Scope Inventory

## Workbook Checklist

- Planned workbook path: `C:\repositories\CanDoItAll\codex\bundles\page-refactor-component-extraction\inventories\page-refactor-checklist.xlsx`
- Workbook sheets: `Summary`, `Route Pages`, `Large Components`, `Subbundles`, `Validation`.
- Workbook purpose: durable checklist with route/file references, line metrics, refactor type, event/state risk, owning subbundle, proof commands, and execution status.

## Route Pages Reviewed

| Route | File | Refactor decision | Subbundle |
| --- | --- | --- | --- |
| `/prompt-factory` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor` | helper extraction then shell component split | `03`, `04` |
| `/projects/{ProjectId:guid}/structure` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` | `ProjectStructureNodeHelpers` first, then shell components | `01`, `02` |
| `/plugins` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor` | helper and render-fragment extraction | `05` |
| `/crm-hr/directory` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrDirectoryPage.razor` | filter/editor helper extraction | `06` |
| `/crm-hr/crm` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrCrmPage.razor` | filter/editor helper extraction | `06` |
| `/crm-hr/workforce` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrWorkforcePage.razor` | filter/format helper extraction | `06` |
| `/crm-hr/recruiting` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrRecruitingPage.razor` | medium helper cleanup if checklist confirms value | `06`, `09` |
| `/projects` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor` | medium cleanup after high-risk phases | `09` |
| `/scheduler` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\Pages\SchedulerPlannerPage.razor` | medium cleanup after high-risk phases | `09` |
| `/prompt-gallery` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor` | nested catalog helper cleanup | `09` |
| `/agents/workflows` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor` | large markup shell review with WorkflowCanvasEditor | `08` |
| `/settings` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor` | host page stable, extract long panel helpers | `07` |
| `/test-lab` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab\Pages\TestLabPage.razor` | medium cleanup if checklist confirms value | `09` |
| `/validation` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation\Pages\ValidationCenterPage.razor` | medium cleanup if checklist confirms value | `09` |

## Long Page-Owned Components Reviewed

| Component | Refactor decision | Subbundle |
| --- | --- | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor` | split markup regions into focused editor components | `08` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor` | extract helper logic and consider section components | `07` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor` | split dialog clusters after node helpers land | `02` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureProcessAssignmentDialog.razor` | split assignment regions after node helpers land | `02` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\StorageSettingsPanel.razor` | extract storage helper logic | `07` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectModalHost.razor` | review during remaining route cleanup | `09` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor` | review during remaining route cleanup | `09` |

## Reviewed But Lower Priority

- Small product route pages under roughly 300 lines: `/activity`, `/automation`, `/agents`, `/resources`, `/processes`, `/processes/live`, `/projects/{ProjectId:guid}/processes`, `/projects/{ProjectId:guid}/processes/live`, `/not-found`, and `/Error`.
- Component sandbox catalog pages are inventoried in the workbook but are not product-route refactor priorities unless they block BaseLib/CanvasLib proof.
