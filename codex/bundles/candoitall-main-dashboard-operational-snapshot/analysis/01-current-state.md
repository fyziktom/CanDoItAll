# Current State

Prepared from direct source inspection on 2026-07-22. CodeAnalytics MCP was unavailable; therefore no automated snapshot ID, finding inventory, or cycle report is claimed.

## Home And UI

- `repo://src/App/CanDoItAll.Web/Components/Pages/Home.razor` is a 203-line single Razor component serving `/` and `/dashboard`. It injects `NavigationManager`, `WorkbenchStateService`, and `ProjectsService`, loads every project just to count them, listens to workbench state, and renders Projects/Prompt Gallery/Test Lab/Settings quick actions plus workbench-oriented panels.
- The page already uses BaseLib `PageScaffold`, `PageHeader`, `CompactStat`, `Grid`, `SectionCard`, `Stack`, `SelectionListItem`, and `EmptyState`.
- `repo://src/UI/CanDoItAll.AppComponents/Components` is the correct app-wide wrapper boundary. Existing examples include `AgentAvatarActionButton.razor` and `AppTabStrip.razor`.
- `repo://src/App/CanDoItAll.Web/CanDoItAll.Web.csproj` already references AppComponents, Projects, AgentFramework module/workflow abstractions, Process Application, and Process Projections. No new project reference is required.
- Existing routes are `/projects`, `/agents`, `/processes/live`, and `/scheduler`; `repo://src/App/CanDoItAll.Web/Composition/ShellNavigation.cs` is a current route source.

## Data Sources

| Concern | Current source | Current cost/problem | Prepared target |
| --- | --- | --- | --- |
| Projects | `ProjectsService.ListAsync` in `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs` | Loads all projects, hierarchy metrics, phase counts, and portfolio contexts before sorting in memory. | Dedicated typed recent-project query using `AsNoTracking`, projection, deterministic recency order, and `Take(5)`. |
| Workflow activity | `WorkflowOverviewQueryService` and `IWorkflowOverviewStore` | Aggregate path loads definitions and performs grouped overview queries; run paging accepts only one state. | Dedicated workflow dashboard activity service/store query for `Running` + `WaitingForInput`, otherwise latest five, no aggregate overview call. |
| Process activity | `ProcessRuntimeProjectionQueryService` | Full mode can reconcile activity and enrich actions/steps/diagnostics; even ListOnly reads/deserializes up to 500 snapshots before final `Take`. | Dedicated canonical runtime-state query selects active-or-recent IDs/status/update time first; projection display reads are limited to those five IDs and expose lag, with no enrichment. |
| Agent totals | `IAgentFrameworkWorkspaceService.GetAgentOverviewAsync` | Also loads catalog/execution summaries and maps agent/provider/model rows. | `IAgentUsageTotalsQueryService` over `ISandboxWorkspaceStore.LoadUsageProjectionAsync`, returning only `TotalTokens`, `KnownCostUsd`, `UpdatedAtUtc`. |

## Cache Identity And Composition

- `repo://src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs` exposes the active profile resolver; `IDatabaseRuntimeState.GetSnapshot()` exposes the profile ID, fingerprint, and generation identity used by the cache.
- `repo://src/Foundation/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs` currently initializes `Generation` to `0` for the process lifetime. All three identity fields remain mandatory so reuse fails closed when profile/runtime semantics evolve.
- Existing module service-collection extensions register Projects, AgentFramework stores/workflow services, and Process services as scoped boundaries. The Web cache/service/load runner are singleton because the snapshot is global operational data. The load runner is the sole lifetime adapter: it owns a fresh async scope and resolves the scoped loader for each actual refresh.

## Relevant Source Size And Architecture Pressure

| Source | Inspected size | Relevance |
| --- | ---: | --- |
| `Home.razor` | 203 lines | Must shrink to rendering/timer/orchestration and one snapshot dependency. |
| `ProjectModels.cs` | 1006 lines | Do not add the new query to `ProjectsService`; use a top-level query type. |
| `PersistentWorkflowStores.cs` | 2598 lines | Persistent run store is the existing store implementation seam; no partial split or new reference. |
| `ProcessRuntimeProjectionQueryService.cs` | 1907 lines | Do not add another responsibility to this large service; use a cohesive dashboard query type over projection contracts. |
| `AgentFrameworkWorkspaceService` main file | 280 lines plus two partial facade files | Do not add another partial; a top-level usage-totals query avoids the broad constructor and partial cluster. |

## Shared Component Evidence

- Components library catalog correlation: `corr_2fd487b0f97b46328ad7eacec10cd979`.
- Dashboard recommendation correlation: `corr_20b04f5166344e88bdc8307fdd49747f`.
- BaseLib is already configured and is the selected library. Shortlisted components inspected with usage/examples: `PageScaffold`, `PageHeader`, `Grid`, `Card`, `Button`, `Icon`, `Stack`, `SectionCard`, `CompactStatStrip`, `CompactStat`, `Tabs`, `TabsItem`, `SelectionListItem`, `LoadingState`, `EmptyState`, and `Alert`.
- The composition rules favor supporting stats in the header, mutually exclusive activity in tabs, whole-card navigation for one destination, and explicit loading/empty/error feedback.

## Existing Tests And Gaps

- Existing: `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkflowOverviewQueryServiceTests.cs` covers aggregate overview bounds, not active-first fallback.
- Existing: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs` covers process projection and snapshot-only/list-only behavior, not dashboard active-first fallback.
- Existing: `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs` covers usage projection and overview totals.
- Existing: `repo://tests/Components/CanDoItAll.Tests.Components/WorkflowOverviewPanelTests.cs` and `ProcessWorkspaceShellTests.cs` cover neighboring UI, not Home.
- Gap: the Home capsule names `HomePageTests`, but no such file exists.
- Gap: `AppSmokeTests.Dashboard_and_project_creation_flow_work` navigates through project creation and does not assert dashboard content.
- Gap: no pre-existing test covers five-minute expiry, profile-ID/fingerprint/generation cache separation, force bypass, cross-circuit coalescing, fresh-scope runner lifetime, failed refresh retention, countdown, or automatic refresh disposal.

## Current-State Conclusion

The requested behavior fits existing dependencies and module boundaries. The smallest maintainable change is four narrow query seams feeding a scoped loader through one typed singleton lifetime runner and app-process singleton snapshot service/cache, followed by a thin Home rewrite and an AppComponents navigation wrapper. Reusing the broad project, workflow overview, process workspace, or full agent overview paths would meet the visual request while violating the explicit performance contract.
