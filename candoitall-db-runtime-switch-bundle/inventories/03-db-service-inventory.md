# Database Service Inventory

## Direct `IDbContextFactory<AppDbContext>` Consumers

| Source | Role | Runtime-Switch Relevance |
| --- | --- | --- |
| `src/CanDoItAll.Modules.Activity/ActivityModels.cs` | Activity service | Fresh DbContext per operation; good dynamic-factory candidate |
| `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs` | Prompt factory workflows | Uses DB plus managed media; affected by storage switching |
| `src/CanDoItAll.Modules.Projects/ProjectModels.cs` | Project CRUD and hierarchy | Seed target for switch/isolation proof |
| `src/CanDoItAll.Modules.Prompts/PromptModels.cs` | Prompt gallery/versioning | Cross-profile isolation target |
| `src/CanDoItAll.Modules.Resources/ResourceModels.cs` | Resource library | Cross-profile isolation target |
| `src/CanDoItAll.Modules.Security/SecurityModels.cs` | Secret CRUD | Shows why DB-profile secrets cannot live in selected DB |
| `src/CanDoItAll.Modules.TestLab/TestLabModels.cs` | Test lab CRUD | Cross-profile isolation target |
| `src/CanDoItAll.Modules.Validation/ValidationModels.cs` | Validation runs/findings | Cross-profile isolation target |
| `src/CanDoItAll.Modules.Workbench/ProjectStructureAnalyticsService.cs` | Structure analytics | Must see switched DB instantly |
| `src/CanDoItAll.Modules.Workbench/ProjectStructureLeaseService.cs` | Structure leases | Must respect switch gate and active profile |
| `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` | Structure/calendar/object graph | Contains stale-route failure points today |
| `src/CanDoItAll.Modules.Workspace/ProjectStructureAgentAdministrationService.cs` | Agent settings profiles | Cross-profile isolation target |
| `src/CanDoItAll.Modules.Workspace/ProviderExecution.cs` | Provider execution history/settings | Must follow active DB |
| `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` | Workspace settings/provider profiles | DB-backed workspace settings remain per selected DB |
| `src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs` | Background job tracking | Needs profile-aware runtime state/queue guardrails |
| `src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs` | Search index data | Must use active DB on next operation |
| `src/CanDoItAll.Web/Program.cs` | Startup bootstrap | Must stop assuming fixed provider and fixed schema path |

## Implications

- Most services are already short-lived DbContext consumers, which is favorable for runtime switching.
- The big risks are not long-lived contexts; they are stale browser state, fixed startup middleware, and the lack of a switch coordinator.
- Any service with host/file interactions must move in lockstep with storage-root switching, not just DB switching.
