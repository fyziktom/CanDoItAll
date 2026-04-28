# Current State

## ProjectStructure MCP Token Storage

- The ProjectStructure MCP client in `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\ProjectStructureHttpClient.cs` sends the configured agent token to the central CanDoItAll API.
- The API-side token records are database-scoped in `Workspace_ProjectStructureAgentProfiles`.
- The entity is `ProjectStructureAgentProfileRecord` in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\ProjectStructure\ProjectStructureAgentAdministrationModels.cs`; its `AccessTokenCipherText` field stores the protected token.
- `ProjectStructureAgentAdministrationService` in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\ProjectStructure\ProjectStructureAgentAdministrationService.cs` decrypts for edit/admin flows and authorizes incoming MCP calls by comparing the incoming token against profile records.
- Therefore switching the active runtime database makes the token appear empty when the target database has no matching ProjectStructure profile rows.

## Database Profile Switching

- Database profile models and services are in `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\DatabaseProfileModels.cs`.
- Explicit source/target database access is available through `ISwitchableAppDbContextFactory.CreateDbContextForProfileAsync` in `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\SwitchableAppDbContextFactory.cs`.
- Runtime switching and context leases are handled by `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\DatabaseRuntimeSwitching.cs`.
- Workspace DB profile UI orchestration is in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Database\DatabaseProfileWorkspaceService.cs`.

## Existing UI

- The database-management panel is `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor`.
- The startup/current-database modal is `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutDatabaseDialog.razor`.
- Shared component guidance supports `Dialog`, `Stack`, and `Grid` for the required modal layout.

## Candidate Settings Groups

- ProjectStructure MCP token/settings: Workspace module, database tables.
- AI providers: Workspace provider profiles plus Security secrets.
- AI agents: AgentFramework sandbox catalog under profile workspace root, not Workspace DB tables.
- Processes: Processes module definition tables, not runtime run tables.

## Module Boundary Signal

- `CanDoItAll.Modules.Workspace` does not reference `CanDoItAll.Modules.AgentFramework` or `CanDoItAll.Modules.Processes`.
- Generic abstractions should live below modules, likely Infrastructure control-plane, with module-specific transfer handlers registered from each module.
