# 02-page-and-tab-stat-migration

## Status

- `Completed`

## Objective

- Migrate targeted production page headers and tab/subpage stat rows from large stat tiles/cards to compact badge stats and icon-only header actions.

## Covered Inputs

- N002, N003, N004, N005, N006, N007, N008

## Prerequisites

- `01-shared-compact-header-primitives` completed and closure gate passed.
- Shared CSS generated and buildable.

## Exact Source References

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
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrHomePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrDirectoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrCrmPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrWorkforcePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrRecruitingPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAgentsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAssignmentsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\AccountSummaryPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceAnalyticsTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsSummarySection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsOperatorConsoleSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDiagnosticsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCapabilitiesPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentGovernancePanel.razor`

## Deliverables

- Page header stats moved into `PageHeader` stats slots where practical.
- Top-level page `SummaryTiles` removed or converted to compact stat strips.
- CRM-HR tabs converted to compact badge stats.
- Selected tab/subpage `SummaryTiles`/`MetricCard` stat rows converted to compact stat strips.
- Migrated header actions use `PageHeaderActionButton`.

## Dependency Impact

- Final browser proof depends on this sweep. Missing a targeted route would leave N002/N007 partially solved.

## Validation Depth

- UI migration with build proof, inventory proof, and representative large-screen browser proof.

## Implementation Steps

1. Convert page headers and first-screen summary rows in the affected inventory.
2. Convert CRM-HR tab pages first because they are explicitly called out.
3. Convert selected process and AgentFramework tab/subpage stat rows.
4. Convert changed page-header actions to icon-only shared action buttons.
5. Run inventory grep for remaining targeted `SummaryTiles` or `MetricCard` rows and record any explicit exceptions.

## Scope Exceptions

- Dialog-only metric cards and overlay metrics may remain if they are not page header or tab summary surfaces.
- Medium/mobile layout tuning is deferred.

## Do Not Do

- Do not change services, persistence, commands, or route ownership.
- Do not remove useful stat details; move detail text into tooltips.
- Do not introduce page-local duplicate tooltip delay policies.

## Acceptance Checklist

- CRM-HR page tabs no longer render large `SummaryTiles`.
- Targeted non-CRM production pages no longer show first-screen large `SummaryTiles`.
- Migrated header actions are icon-only.
- Compact stats preserve values and helper detail through tooltip text.

## Proof Required

- Build proof after migration.
- Inventory grep showing remaining `SummaryTiles`/`MetricCard` rows are either non-production, dialog-only, overlay-only, or explicitly deferred.
- Large-screen screenshots for `/crm-hr`, at least one CRM child route, and representative non-CRM pages.

## Browser Validation Logging

- Routes: `/crm-hr`, `/crm-hr/directory`, `/automation`, `/validation`, plus any route that looks risky after implementation.
- Viewport: at least 1600x900 or maximized equivalent.
- Actions: navigate, capture full-page or viewport screenshots, hover one stat/action per representative route where feasible.
- Screenshot review questions: is the stat area badge height, do title/stats/actions fit cleanly, are secondary tabs not pushed far down, are tooltips readable and unclipped?

## Progression Gate

- Final browser proof may start only after build passes and the inventory sweep has no unexplained targeted large stat rows.

## Suggested Agent Prompt

```text
Implement subbundle 02 only after subbundle 01 passes. Migrate targeted production page and tab stat rows to PageHeader stats or CompactStatStrip, convert header actions to PageHeaderActionButton, preserve behavior, run build/inventory proof, and update execution report rows. Stop if a targeted page cannot be migrated without overflow or behavior changes.
```
