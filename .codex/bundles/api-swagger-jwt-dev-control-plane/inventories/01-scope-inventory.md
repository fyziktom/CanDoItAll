# Scope Inventory

| Area | Existing source | Target surface | Notes |
| --- | --- | --- | --- |
| Projects | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs` | `/api/dev/projects` | List, get editor, save, delete, hierarchy, subproject links. |
| Project structure | `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs` | `/api/project-structure-mcp` | Keep existing rich MCP-compatible API; add auth and docs coverage. |
| Process definitions | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.cs` and persistence partials | `/api/dev/processes/definitions` | List, editor get, save, publish, delete, import/export. |
| Process runs | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.Reads.cs` and runtime partials | `/api/dev/processes/runs` | List, filtered detail, start, stop, transitions, rerun, assignments, artifacts, direct messages, manager directives. |
| Launch plans and HR matching | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Staffing.cs` | `/api/dev/processes/launch-plans` | Create, list, detail, select candidates, HR match, submit/decide approval, provision, execute. |
| Process templates | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Templates\ProcessTemplateCatalogService.cs` | `/api/dev/processes/templates` | List, detail, mermaid, import, baseline scenarios. |
| Agents | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs` | `/api/dev/agents` | List, editor, save, delete, clone, chat, execution run history, artifacts. |
| Settings token UI | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor` | `/settings?tab=api-access` | Show active/inactive auth, token creation form when active. |
