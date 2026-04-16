# Current State

## Repository Findings

- The dedicated Agents workspace uses the technical agent workspace service as its primary inventory source. `AgentsHomePage.razor.cs` and `AgentCatalogPanel.razor.cs` load agent summaries from `IAgentFrameworkWorkspaceService`.
- CRM-HR currently lists agents through `AiAgentService.ListAgentDirectoryAsync()` in `CrmHrServices.cs`. That method first queries `Party` rows where `PartyType == PartyType.AiAgent` and returns an empty list when none exist, even if technical agents already exist in the agent framework.
- `AiTechnicalAgentBridge` already knows how to map CRM-HR parties to technical agents and can resolve pending backfill through metadata. The defect is not missing bridge infrastructure. The defect is that CRM-HR directory listing starts from the wrong inventory.
- The `/processes` route is a thin host over `ProcessWorkspace`. The workspace uses a fill-height `PageScaffold`, a `ListDetailShell`, and tab panels with auto overflow. The mobile-only CSS exception in `ProcessWorkspace.razor.css` suggests the desktop containment bug is in the root shell layout rather than in the tab content itself.
- The database profiles dialog renders the active selection workspace root and a selectable list of saved profiles, but it does not expose copy affordances for any of the visible file-system paths.

## Template And Showcase Findings

- A real process-template system already exists in `src\CanDoItAll.Modules.Processes`. `ProcessTemplatePackLoader`, `ProcessTemplateProjectionService`, and `ProcessDevelopmentSeedService` can project definitions from `Templates\Processes` and seed baseline runs.
- `Templates\Processes\processes\software-delivery\definition.json` already models a rich software-delivery flow with role-first steps, approvals, QA, and release-oriented artifacts.
- `Templates\Processes\seed-catalog\baseline-scenarios.json` provides baseline scenario metadata, but the current scenario seeder tool still contains hardcoded integration simulation logic. The requested showcase must move toward the template-driven path instead of extending the hardcoded simulation.

## Validation Surface

- Existing tests already cover CRM-HR agent workflows, database profile UI, process workspace browser flows, and projected process nodes in project structure. These are good anchors for the first implementation wave.
- The user-provided showcase database is a real managed profile database. The run must be careful about collisions and should use distinct showcase naming rather than destructive resets.
