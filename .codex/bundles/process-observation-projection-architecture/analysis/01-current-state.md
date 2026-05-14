# Current State

## What Exists

- `ProcessesPage.razor` is a thin route that renders `ProcessWorkspace`.
- `ProcessWorkspace` is the current page-level coordinator. It injects `ProcessesService`, `ProcessWorkspaceRunDetailsLoader`, `ProcessRuntimeStateOverviewService`, `DialogService`, `IProcessEscalationService`, `IAgentFrameworkWorkspaceService`, project services, template services, and canvas factories.
- `ProcessWorkspace` owns a large mutable state surface: definitions, run lists, selected run details, active summaries, analytics, improvements, launch plans, assignments, outbox records, decisions, execution runs, escalations, approvals, and canvas state.
- `ProcessWorkspace.LiveRefresh.cs` runs a 4-second `PeriodicTimer` when the selected process has active runs or active launch plans and the user is on Runs or Analytics. Each tick can force runtime overview refresh, reload runtime pane data, refresh the canvas, and call `StateHasChanged`.
- `ProcessWorkspace.Loading.cs` already avoids loading runtime pane data when the active tab does not need it. It loads runtime pane data only for Runs, Analytics, direct run query, or direct launch plan query.
- `ProcessWorkspaceRunDetailsLoader` separates selected-run enrichment from the component. It loads full run details, journal escalations, journal timeline, AgentFramework execution runs, and then builds health, operator approvals, and attempt timeline.
- `LoadActiveRunSummariesAsync` is already a lightweight active-run path. It filters active runs, batches process-side health metrics through `GetActiveRunHealthMetricsAsync`, scans recent AgentFramework execution runs with a cap, and avoids full selected-run details for every active run.
- `ProcessRuntimeReadQueryService` provides existing read-model queries for run lists, step runs, run details, active run health metrics, and analytics. It uses `AsNoTracking` heavily and has several batch projection paths.
- `ProcessRuntimeStateOverviewService` is a small scoped cache for run status counts by definition. It caches one normalized definition-id set and project id, and exposes `Invalidate()`, but the live refresh path usually calls with `forceRefresh: true`.
- `ProcessOperatorControlPlane` and `ProcessOutbox` already carry process-operational facts that the future dashboard will need to observe.
- The UI uses BaseLib and CanvasLib components. No Radzen package reference was found. Tailwind utility classes are already used in the Processes UI.

## Current Performance Positives

- Previous UI work moved active-run summaries away from repeated full-detail reads.
- Runtime read paths use `AsNoTracking` for observation-style queries.
- Run detail loading is selected-run scoped.
- Runtime pane loading is tab-aware.
- AgentFramework execution run scan for active summaries is capped.

## Gaps

- The observation boundary is still page-local. `ProcessWorkspace` owns refresh scheduling, data shape selection, and many projection decisions.
- There is no reusable multi-process observation service for a dashboard that spans process definitions, stages, health, activity, outbox, and AgentFramework status.
- Dialog-ready detail payloads are not represented as typed descriptors. The page loads selected-run details rather than asking a read-only observation service for a specific drill-down payload.
- Existing caching is not a general observation cache. It has no size policy, no per-entry TTLs, no key-space controls, no per-key stampede protection, and no explicit staleness result.
- The current 4-second page timer can become expensive when many processes are running because refresh still fans out through run lists, active summaries, analytics, canvas refresh, and full selected-run detail reloads.
- A future AI dashboard cannot safely change UI state today without binding directly to component state or free-form strings.

## Constraints Learned From The Code

- Current functionality spans authoring, launching, operator controls, runtime canvas, assignments, direct messaging, artifacts, analytics, and manager chat. Any migration must be incremental.
- Process core must remain generic. Process-specific behavior belongs in process definitions, step instructions, agent tools, or skills.
- Existing components already use project scoping; observation keys must include project scope and later user/authorization scope.
- Existing `ProcessWorkspace` presenter types are coupled to the component. A future observation shell should not make this coupling worse.
