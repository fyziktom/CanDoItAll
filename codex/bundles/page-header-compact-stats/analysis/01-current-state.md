# Current State

## Processes Reference

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor` already uses a compact `Toolbar` header (`processes-command-strip`) instead of the normal `PageHeader`.
- Its top row is page eyebrow + `StatusBadge` chips + icon-only `Button`s with `title` and `aria-label`.
- It still needs the requested shared tooltip/delay treatment because the existing process badges are plain `StatusBadge`s and the icon buttons rely on title text instead of the shared tooltip primitive.

## Shared Component Baseline

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\PageHeader.razor` is the central reusable page-header primitive.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Badges\StatusBadge.razor` is the existing compact chip primitive.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\TooltipTarget.razor` already supports delayed hover/focus tooltips through the `Delay` parameter.
- `C:\repositories\CanDoItAll\Tailwind\navigation\page-header.css` and `C:\repositories\CanDoItAll\Tailwind\layout\stats.css` own generated shared styling.

## Affected Production Inventory

Large `SummaryTiles` or `MetricCard` summaries were found in production page and subpage surfaces:

- Page headers or first-screen summary rows:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Pages\Home.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Pages\AutomationPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Collaboration\Pages\CollaborationHomePage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\Pages\SchedulerPlannerPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation\Pages\ValidationCenterPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab\Pages\TestLabPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectCalendarPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- CRM-HR tabs with first-screen stats:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrHomePage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrDirectoryPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrCrmPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrWorkforcePage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrRecruitingPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAgentsPage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAssignmentsPage.razor`
- Tab/subpage metric rows:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\AccountSummaryPanel.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceAnalyticsTab.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsSummarySection.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsOperatorConsoleSection.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDiagnosticsPanel.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCapabilitiesPanel.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentGovernancePanel.razor`

Dialog-only or overlay metric rows exist in project/workbench/process modal code. They are lower priority unless browser proof shows they materially affect page header or tab height.
