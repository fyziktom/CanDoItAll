# Scope Inventory

## Route Inventory And Initial Visual Proposals

The route inventory is intentionally large-screen focused. Each implementation subbundle must refresh the row after capturing its baseline screenshot and, where the page is materially changed, add an `imagegen` planning prompt or note why the accepted page/tab/dialog proposal is sufficient.

Detailed current elements, UX flows, tabs, dialogs, and proposal coverage live in `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs`.

| Route or surface | Source file | Current pattern | Initial proposal | Owner |
|---|---|---|---|---|
| `/`, `/dashboard` | `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Pages\Home.razor` | Dashboard PageScaffold with summary tiles and card sections. | Compress header, keep quick actions and attention queue as dense workbench bands, remove redundant explanatory copy. | SB04 |
| `/projects` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor` | Board/card-first with modal flows and hierarchy modal. | Add TreeView portfolio/hierarchy navigation plus detail pane; keep modal editor for deep edits. | SB03 |
| `/projects/{ProjectId}/structure` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` | Dense project structure workbench, existing support tree in components. | Preserve canvas/workbench focus; align shell width and move secondary metadata into dialogs/flyouts. | SB03 |
| `/projects/{ProjectId}/calendar` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectCalendarPage.razor` | Calendar page with cards. | Use full-width calendar workspace and dialog detail for event metadata. | SB03 |
| `/processes` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor` | Thin wrapper over `ProcessWorkspace`. | Tree-driven global process library entry. | SB03 |
| `/projects/{ProjectId}/processes` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProjectProcessesPage.razor` | Thin wrapper over `ProcessWorkspace`. | Tree-driven project process library scoped to active project. | SB03 |
| Process workspace | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor` | Dense ListDetailShell with flat definition list and page-local CSS. | Replace definition list with TreeView grouping by scope/status/project/subprocess; retain tabs and dialogs. | SB03 |
| `/processes/live`, `/projects/{ProjectId}/processes/live` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\LiveProcessesPage.razor` | Thin wrapper over live dashboard. | Full-width live operations screen with compact filters and dialogs for activity details. | SB03 |
| Live processes dashboard | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\LiveProcessesDashboard.razor` | Large live dashboard with many cards, dialogs, charts. | Keep tabs, compress command strip, move detail-heavy cards into dialogs, ensure no width is lost to shell chrome. | SB03 |
| `/agents` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor` | PageScaffold with secondary tabs. | Dense provider/agent workspace with minimal header and hover help. | SB04 |
| `/agents/workflows` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor` | Large workflow page with dialog signals. | TreeView for workflow definitions, versions, components, runs; detail pane for selected node. | SB03 |
| `/resources` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor` | PageScaffold resource list. | Full-width resource table/list with concise filters and details in dialog. | SB04 |
| `/plugins` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor` | Large plugin page. | Tree/group plugins by state/source/capability; move package details to dialog/inspector. | SB04 |
| `/prompt-gallery` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor` | PageScaffold with prompt lists/cards. | Tree/group prompts by collection/version/tag; reduce large cards. | SB04 |
| `/prompt-factory` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor` | Very large focus page without PageScaffold. | Preserve focused workbench; use full width and move advanced session metadata into dialogs/floating inspectors. | SB04 |
| `/settings` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor` | PageScaffold settings tabs; DB settings panel lives here. | Keep as detailed settings destination, but make shell DB action link here and remove topbar DB switch. | SB04 |
| Settings DB panel | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor` | Database profile management panel. | Support shell flyout state and safe copy summary without duplicating management UI. | SB02 |
| `/crm-hr` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrHomePage.razor` | PageScaffold CRM/HR overview. | Compact B2B module hub; no hero-like marketing copy. | SB05 |
| `/crm-hr/directory` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrDirectoryPage.razor` | Large directory page with dialogs. | Tree/list directory grouping with inspector dialogs for party details. | SB05 |
| `/crm-hr/crm` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrCrmPage.razor` | Large CRM page with dialogs. | Pipeline/list-detail density pass; move account/opportunity details to dialogs. | SB05 |
| `/crm-hr/workforce` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrWorkforcePage.razor` | Large workforce page. | Dense allocation/workforce workbench with compact cards and dialog details. | SB05 |
| `/crm-hr/recruiting` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrRecruitingPage.razor` | Recruiting page. | Pipeline workspace with dialog-based candidate detail. | SB05 |
| `/crm-hr/agents` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAgentsPage.razor` | Agent page. | Compact agent list/detail with hover details. | SB05 |
| `/crm-hr/assignments` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAssignmentsPage.razor` | Assignment page. | Full-width assignment planning grid with details in dialog. | SB05 |
| `/collaboration` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Collaboration\Pages\CollaborationHomePage.razor` | Collaboration PageScaffold. | Inbox/work queue density pass; dialogs for conversation metadata. | SB05 |
| `/activity` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\Pages\ActivityPage.razor` | Activity page. | Full-width timeline/search with compact filters. | SB05 |
| `/automation` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Pages\AutomationPage.razor` | Automation PageScaffold with cards. | Operational jobs table/list with dialogs for run details. | SB05 |
| `/scheduler` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\Pages\SchedulerPlannerPage.razor` | Scheduler page with DataGrid and dialogs. | Full-width schedule/workflow planning surface; preserve dialogs. | SB05 |
| `/validation` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation\Pages\ValidationCenterPage.razor` | Validation center PageScaffold. | Dense validation run list/detail with dialogs for findings. | SB05 |
| `/test-lab` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab\Pages\TestLabPage.razor` | Test lab PageScaffold. | Test run workspace with compact status and dialog details. | SB05 |
| `/not-found`, `/Error` | `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Pages\NotFound.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Pages\Error.razor` | Minimal error pages. | Keep minimal; verify shell chrome does not make them look broken. | SB06 |

## Page Input And Proposal Map

| Page input | Proposal asset | Owning subbundles |
|---|---|---|
| Shell and database controls | `evidence/design-proposals/pages/01-shell-baselib-corrected-proposal.png` | SB00-02, SB02 |
| Dashboard and projects | `evidence/design-proposals/pages/02-project-pages-tabs-dialogs-proposal.png`, `evidence/design-proposals/pages/05-core-pages-tabs-dialogs-proposal.png` | SB01, SB03, SB04 |
| Process workspace and live processes | `evidence/design-proposals/pages/03-process-pages-tabs-dialogs-proposal.png` | SB03, SB03-04 |
| Agents and workflows | `evidence/design-proposals/pages/04-agent-workflow-tabs-dialogs-proposal.png` | SB03, SB03-04, SB04 |
| Prompts, plugins, settings, resources | `evidence/design-proposals/pages/05-core-pages-tabs-dialogs-proposal.png` | SB04, SB04-05 |
| CRM/HR and supporting operations | `evidence/design-proposals/pages/06-supporting-pages-tabs-dialogs-proposal.png` | SB05, SB05-06 |
| Reusable BaseLib components | `evidence/design-proposals/pages/07-baselib-reusable-components-proposal.png` | SB00-02, SB00-03 |

## Reference Pattern Inventory

| Pattern | Reference | Planned CanDoItAll use |
|---|---|---|
| Collapsed icon rail | `C:\repositories\CanDoItAll.Economy\src\CanDoItAll.Economy.Simulator.Components\Components\EconomySimulatorAppShell.razor` | `AppShell` collapsed default plus tooltips and bottom actions. |
| BU tree/detail split | `C:\repositories\CanDoItAll.Economy\src\CanDoItAll.Economy.Simulator.Components\Components\BusinessUnitTree.razor` | Project/process/workflow tree surfaces. |
| Run observation density | `C:\repositories\CanDoItAll.Economy\src\CanDoItAll.Economy.Simulator.Components\Components\SimulationRunObservationPage.razor` | Live processes, process workspace, validation, scheduler, and dashboard density target. |

## Component Inventory

| Component | Source | Use |
|---|---|---|
| `AppShell` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor` | Shell refactor foundation. |
| `TooltipTarget` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\TooltipTarget.razor` | Collapsed rail hover/focus help. |
| `TreeView` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TreeView.razor` | Projects/processes/workflows hierarchy. |
| `PageScaffold` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\PageScaffold.razor` | Full-width desktop page workspaces. |
| `DialogScaffold` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\DialogScaffold.razor` | Dense dialog bodies. |
| `ListDetailShell` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Lists\ListDetailShell.razor` | Tree/detail and list/detail workspaces. |
| `Tabs` / `SecondaryTabs` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation` | Dense tab bodies and dialog tab content. |
| `SummaryTiles` / `MetricCard` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Cards` | Compact metric strips. |
| `Toolbar` / `FilterBar` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation` | Search, filters, icon actions, overflow actions. |

See `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inventories\02-reusable-baselib-component-candidates.md` for reusable component foundation details.
