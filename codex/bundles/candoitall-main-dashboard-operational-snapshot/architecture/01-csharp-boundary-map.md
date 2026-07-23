# C# Boundary Map

| Boundary | Contract / DTO owner | Implementation owner | Must not own |
| --- | --- | --- | --- |
| Dashboard snapshot | `CanDoItAll.Web` app-level namespace | Singleton `DashboardSnapshotService`/cache/load runner plus scoped thin loader in Web | EF, file/projection stores, navigation/rendering, user-specific data |
| Recent projects | Projects module public read contract | Projects module query implementation | hierarchy/portfolio mapping or writes |
| Workflow activity | Workflow Abstractions | Workflow Core service plus persistent/in-memory activity store implementation | aggregate overview/group counts |
| Process activity | Processes Runtime/Application/Projections existing boundary | Processes activity service registered by Processes module | unbounded projection scans, assignment/diagnostic/history/agent/usage enrichment |
| Agent totals | AgentFramework Core | AgentFramework Core query registered by AgentFramework module | full overview/catalog/model mapping |
| Quick action | `CanDoItAll.AppComponents` parameters | `QuickActionCard.razor` | dashboard routes/policy/data loading |
| Home | Web Razor page | rendering, state, timer lifecycle | caching, query policy, persistence |

## Target Top-Level Types

- Singleton `DashboardSnapshotService`, `DashboardSnapshotCache`, and `IDashboardSnapshotLoadRunner`; scoped `IDashboardSnapshotLoader`; immutable `ImmutableArray<T>` snapshot/key/row/activity-mode records with hard-five validation.
- Dedicated recent-project query interface/implementation and thin row.
- `IWorkflowDashboardActivityQueryService` and activity store contract/query/result; persistent and in-memory store support.
- `IProcessDashboardActivityQueryService` selects IDs/status/update time from canonical runtime state and optionally reads projection display fields for only those IDs.
- `IAgentUsageTotalsQueryService`, implementation, and totals snapshot.
- `QuickActionCard` Razor component.

Final names may follow local namespace conventions but responsibilities may not merge back into Home or the large existing services.

## Composition Root Responsibilities

- Existing module extensions register their typed query contracts as scoped and reuse established store instances.
- Web `Program.cs` registers the loader as scoped and the service/cache/load runner as singleton.
- `DashboardSnapshotLoadRunner` alone uses `IServiceScopeFactory`/the created scope's `IServiceProvider` to resolve one fresh loader per actual refresh. No provider is passed into UI, query services, loader composition, or cache policy; ordinary factory lambdas may still alias one registered implementation to its interface in existing DI style.

## Old Responsibilities To Remove Or Leave

- Remove Home's workbench subscription/state panels and broad `ProjectsService.ListAsync` count load.
- Leave workbench services, aggregate workflow overview, full process workspace query, and full Agent Overview unchanged for their existing consumers.
- No temporary bridge is needed. If execution introduces one, it must be removed inside SB01 before SB02 unlocks.
