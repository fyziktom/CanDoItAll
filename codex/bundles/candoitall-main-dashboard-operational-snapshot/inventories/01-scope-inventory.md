# Scope Inventory

## Planned Ownership

| Area | Existing anchor | Planned responsibility | Owning SB |
| --- | --- | --- | --- |
| Web dashboard application service | `repo://src/App/CanDoItAll.Web/Program.cs` and Web project | `ImmutableArray<T>` snapshot DTOs with hard-five validation; scoped loader; singleton service/cache/load runner; cross-circuit coalescing; profile-ID/fingerprint/generation key; fresh async scope per actual refresh | SB01 |
| Projects read boundary | `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs` and module DI | `IRecentProjectsQueryService`-style typed bounded projection outside `ProjectsService` | SB01 |
| Workflow read boundary | `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowOverviewContracts.cs`, Core DI, persistent/in-memory run stores | Dedicated dashboard activity query/store contract and implementations; active-first/fallback | SB01 |
| Process read boundary | `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs`, Application, Projections, module DI | Canonical runtime-state active/recent selection, then projection display reads for only the selected five IDs; no enrichment | SB01 |
| Agent usage boundary | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs`, usage model, AgentFramework module DI | `IAgentUsageTotalsQueryService`, typed totals snapshot, store-backed implementation | SB01 |
| Shared quick action | `repo://src/UI/CanDoItAll.AppComponents/Components` | `QuickActionCard.razor` and only minimal scoped styling needed for square/centered anatomy | SB02 |
| Home page | `repo://src/App/CanDoItAll.Web/Components/Pages/Home.razor` | Rendering, UI states, tabs, timer/countdown orchestration, disposal | SB02 |
| Proof | existing Unit/Integration/Components/Playwright projects | Targeted behavioral tests, build, architecture/source assertions, screenshots | SB01–SB03 |

Names ending in “-style” are responsibility descriptions, not permission to choose stringly typed alternatives. Execution records final type/file names in `bundle://reviews/01-execution-report.md`.

## Route Inventory

| Label | Route | Evidence |
| --- | --- | --- |
| Projects | `/projects` | Existing Home and project page navigation |
| Agents | `/agents` | `repo://src/App/CanDoItAll.Web/Composition/ShellNavigation.cs` |
| Live Processes | `/processes/live` | Processes module route/navigation evidence |
| Scheduler | `/scheduler` | `repo://src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor` |

## Shared Components Selected

| Purpose | Shared component path | Decision |
| --- | --- | --- |
| Page boundary/header | `PageScaffold`, `PageHeader` | Reuse current page pattern with compact header. |
| Supporting totals | `CompactStatStrip`, `CompactStat` | Metrics support operational lists; do not promote to metric cards. |
| Quick actions | AppComponents wrapper composed from BaseLib `Card`, `Button` with `Href`, `Icon`, `Stack`, and `Grid` | One unambiguous destination makes whole-card navigation appropriate; wrapper prevents page-local markup repetition. |
| Collections | `SelectionListItem`, `Stack`, `EmptyState`, `LoadingState`, `Alert` | Typed rendered states without custom structural wrappers. |
| Activity modes | `Tabs`, `TabsItem` | Workflow and process lists are mutually exclusive supporting views. |

BaseLib package/setup already exists; no library/package/service/asset change is planned.

## Project Reference Inventory

- Web already references AppComponents, Projects module, AgentFramework module/workflow abstractions, Process Application, and Process Projections.
- Unit already references workflow core/runtime, Projects, Processes, and AgentFramework Core.
- Integration and Components already reference Web and affected modules.
- Playwright already references Web.
- Required change in all project files: none. If compilation reveals a missing direct reference, stop and repair the boundary plan; do not add it as an incidental fix.

## Test Inventory And Planned Additions

| Project | Existing anchor | Planned focused proof |
| --- | --- | --- |
| Unit | `WorkflowOverviewQueryServiceTests.cs`, `ProcessProjectionPipelineTests.cs` | Dedicated workflow/process activity positive/negative/bound tests and no-enrichment counting fake. |
| Integration | `AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs`, Projects integration patterns | Recent project relational query, agent totals projection, app snapshot cache/profile-generation/coalescing tests. |
| Components | neighboring page/panel tests | `QuickActionCardTests`, `HomePageTests` with fake snapshot service/time. |
| Playwright | `AppSmokeTests.cs` | A real dashboard operational snapshot flow at `1440x900`; the existing misnamed project-creation test is not sufficient. |

## Excluded Surfaces

- No other bundle is reopened.
- No Components.BaseLib source/sandbox change is planned; AppComponents owns the product wrapper.
- No workflow/process runtime mutation, scheduler behavior, project portfolio data, Agent catalog mapping, or database-profile switching behavior changes.
- SB01 contains PostgreSQL TEMP-table decision evidence at `bundle://evidence/SB01/postgresql-dashboard-index-plans.md`; its implementation, migration, targeted test, and AC01 proof are complete. SB02 evidence is in progress and SB03/final evidence remains pending.
