# dashboard-snapshot-foundation

## Status

- `Completed`
- Checkpoint: `AC01 Approved`

## Objective

- Establish the bounded, typed data queries and coherent five-minute dashboard snapshot before any Home UI consumes them.

## Success Criteria

- Recent projects are ordered deterministically by update time and limited to five through a no-tracking database projection.
- Workflow and process results return at most five active rows, or the latest five rows only when there are no active rows, with an explicit typed activity mode.
- Token and known-cost totals come only from the canonical AgentFramework usage projection.
- The app-process singleton snapshot service/cache reuses one global operational result across circuits for five minutes, coalesces concurrent callers, isolates database runtime profile ID/fingerprint/generation, throttles failed automatic attempts, and lets explicit force refresh bypass a settled cache entry.
- Each actual refresh uses a typed singleton load runner that owns a fresh async DI scope, resolves the scoped loader there, and disposes the scope after completion. No user-specific data is cached.
- The cached project/workflow/process graph is `ImmutableArray<T>` and every collection has a loader-enforced hard maximum of five.
- A failed or cancelled load is never cached and never silently converted to empty data.

## Covered Inputs

- Requirements `REQ-05` through `REQ-11` and `REQ-15` through `REQ-18`.
- Architecture decisions `PSR-01`, `PSR-02`, and `PSR-03`.
- Performance risks around the broad project list, grouped workflow overview, full process enrichment, and full Agent Overview facade.

## Prerequisites

- Bundle prepared-stage validation passes.
- Architecture checkpoint `AC00` is recorded as passed.
- Existing canonical project, workflow, process runtime-state/projection, AgentFramework usage, and runtime-profile sources remain available.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectsModuleServiceCollectionExtensions.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions/WorkflowOverviewContracts.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowOverviewQueryService.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/InMemoryWorkflowRunStore.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Projections/ProcessProjectionQueries.cs`
- `repo://src/App/CanDoItAll.Web/Program.cs`

## UI Composition Contract

- N/A. This foundation has no browser-visible deliverable; SB02 owns the visible surface.

## Deliverables

- Narrow project, workflow activity, process activity, and agent usage query contracts and implementations in their existing owning projects.
- Immutable dashboard snapshot/data contracts, scoped loader, singleton load runner/cache/service, options, and DI registration in `CanDoItAll.Web`.
- Direct behavioral tests covering ordering, limits, active/fallback selection, canonical usage totals, cache reuse, expiry, force refresh, concurrency, cancellation/fault recovery, and runtime-key invalidation.

## Dependency Impact

- SB02 depends on these contracts for every rendered row, metric, loading state, and countdown. Incorrect policy here invalidates all UI and browser proof.
- SB03 depends on query bounds and cache invocation counts for the performance and architecture gates.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: yes; the entire dashboard data and refresh surface depends on it.

## Implementation Steps

1. Add each bounded source query in its canonical owning module without routing through the existing broad facade.
2. Add deterministic active-or-recent workflow and process selection and an explicit activity mode.
3. Add the typed Web snapshot model with `ImmutableArray<T>` collections, hard-five validation, and thin parallel scoped loader.
4. Add the five-minute, runtime-key-aware singleton cache/service, typed singleton scoped-load runner, and force-refresh enum/API.
5. Register services without adding project or package references; confine scope/provider resolution to the lifetime runner.
6. Add positive and meaningful negative tests, then run the affected project builds/tests.
7. Update the execution report and pass checkpoint `AC01` before SB02 begins.

## Scope Exceptions

- Measured PostgreSQL evidence authorizes only the three dashboard indexes and the workflow `UNION ALL` active-or-fallback shape recorded in `bundle://evidence/SB01/postgresql-dashboard-index-plans.md`; unrelated schema changes remain forbidden.
- Projection display lookup remains limited to the at-most-five canonical runtime IDs; projection lag is explicit and no catch-up loop is added to a dashboard read.

## Do Not Do

- Do not call `ProjectsService.ListAsync`, `IWorkflowOverviewQueryService`, a full process workspace/enrichment query, or `IAgentFrameworkWorkspaceService.GetAgentOverviewAsync` from the dashboard path.
- Do not inject EF contexts, stores, or `IServiceProvider` into Home, query composition, the dashboard loader, or cache policy. Only `DashboardSnapshotLoadRunner` may resolve the scoped loader from its own fresh async scope.
- Do not place user-specific, authorization-sensitive, or per-circuit state in the shared cache.
- Do not sum workflow/process telemetry into AgentFramework totals, return partial silent results, retain faulted tasks, or add project/package references.

## Acceptance Checklist

- [x] Every source result is typed, deterministically ordered, and limited to five where applicable.
- [x] Mixed active/terminal fixtures return only active rows; no-active fixtures return exactly the latest bounded rows.
- [x] Cache hits across scopes, expiry, force refresh, concurrent coalescing, failure throttling, caller-cancellation isolation, and database-runtime changes during load have direct behavioral proof.
- [x] Every actual refresh creates/disposes a fresh async scope and resolves a fresh scoped loader; provider resolution appears nowhere outside the lifetime adapter.
- [x] The loader invokes only narrow query APIs, publishes `ImmutableArray<T>` collections, rejects a sixth row, and preserves cancellation.
- [x] Affected Release builds pass with 0 errors; final whole-solution warning classification remains an SB03 responsibility.

## Proof Required

- Targeted unit/component/integration tests named in `bundle://architecture/04-csharp-testability-plan.md`.
- Source/diff proof that forbidden broad APIs and new project references are absent.
- Invocation-count proof that concurrent and repeated in-TTL reads load once.
- Scope-lifetime proof that shared cross-circuit refreshes never retain/reuse scoped services.
- PostgreSQL TEMP-table plan evidence from `bundle://evidence/SB01/postgresql-dashboard-index-plans.md`, followed by narrow migration/query implementation proof before closure.
- `validate_bundle.py --stage prepared` before entry and subbundle validation before closure.

## Browser Validation Logging

- N/A. This subbundle has no directly rendered UI; SB02/SB03 own browser proof.

## Progression Gate

- `AC01` was approved on 2026-07-22 after Web Release build 0 errors, dashboard query tests 28/28, cache/loader/runner component tests 18/18, and the branch-local workflow correction followed by AgentFramework Release build 0 errors and workflow activity tests 13/13. SB02 is unlocked.

## Closure Evidence

- Behavioral query gate: `28/28` across `DashboardQueryServicesTests`, `ProcessDashboardActivityQueryTests`, and `WorkflowDashboardActivityQueryTests`.
- Cache/composition gate: `18/18` across `DashboardSnapshotCacheTests`, `DashboardSnapshotLoaderTests`, and `DashboardSnapshotLoadRunnerTests`.
- Branch-local workflow correction: AgentFramework Release build `0 errors`; focused `WorkflowDashboardActivityQueryTests` rerun `13/13`.
- Web composition build: Release `0 errors`.
- Architecture decision: `AC01 Approved`; SB01 reopen triggers remain binding and downstream proof must be repeated if later evidence invalidates the foundation.

## Reopen Triggers

- Reopen SB01 and invalidate SB02/SB03 data evidence if any result exceeds five rows, a cached collection is mutable, active and terminal rows are mixed, cache reuse/coalescing fails, a refresh reuses a scoped loader/scope, a runtime-key change reuses stale data, user-specific data enters the cache, or a broad/enriched source appears on the call path.

## Suggested Agent Prompt

```text
SB01 is completed and AC01 is approved. Reopen it only when a documented trigger invalidates the typed query, immutable hard-five snapshot, cache/coalescing, runtime identity, failure, or scoped-load-runner evidence; then re-run every affected downstream proof.
```
