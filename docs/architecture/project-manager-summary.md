# Project Manager Summary

## Decision

Add Manager Summary as the third server-rendered view inside the existing Project Structure workbench tab.
The parent page remains a tab host only. A dedicated panel owns rendering, a dedicated application query composes reporting projections, and a scoped keyed state store retains the last loaded snapshot when the routed or server-rendered panel is disposed.

The report is opt-in. Selecting the tab performs no report query. The first data access happens only after the user selects a time range, history/future mode, and project scope, then chooses **Load summary**.

## Architecture evidence

The focused CodeAnalytics snapshot `snap-20260728235207-34f3be91` covered the ten projects that own Project Structure, project hierarchy, agent execution history, workflow persistence, and process projections. It found no blocking workspace diagnostics.

The snapshot confirms these dependency directions:

- Workbench already depends on Projects, AgentFramework Core, Workflow Abstractions, and Process Projections/Persistence.
- AgentFramework's application module depends on Workbench, so Workbench must not take a reverse dependency on that module.
- `ProjectStructurePage` is an existing hotspot with 652 source members. Manager Summary behavior therefore belongs in new top-level types, not another page partial.

## Responsibility inventory

| Responsibility | Owner | Boundary |
|---|---|---|
| Third-tab selection and project identity | `ProjectStructurePage` | UI host |
| Options, explicit load, progress/error states, charts, recent activity, dialog | `ProjectManagerSummaryPanel` | UI component |
| Per-project retained options and last successful bounded snapshot | `ProjectManagerSummaryStateStore` | Scoped UI state |
| Descendant closure with cycle-safe de-duplication | `ProjectManagerSummaryScopeResolver` | Workbench application query |
| Cross-source query orchestration and paging | `ProjectManagerSummaryQueryService` | Workbench application query |
| Cost ownership, trend merge, latest-activity merge, and warnings | `ProjectManagerSummarySnapshotCalculator` | Pure Workbench calculation |
| Remaining task schedule and expected-cost projection | `ProjectPlanAnalyticsQueryService` and calculator | Workbench application query/domain calculation |
| Direct project agent-run reporting | Versioned agent execution reporting projection | AgentFramework Core/Persistence |
| Standalone project workflow reporting | Indexed workflow-origin projection | Workflow abstraction/persistence |
| Root process reporting | Existing process run-record projection | Process projection/persistence |

The UI never reads EF entities, serialized workflow origins, agent run files, or process persistence entities directly.

## Query and ownership rules

### Project scope

- Current-project scope contains exactly the opened project.
- Recursive scope expands descendants in bounded frontier batches and de-duplicates projects because multiple parents are valid.
- A recursive scope at or above the warning threshold requires explicit confirmation before report sources are queried.
- Empty identifiers, inverted dates, and unsupported page sizes fail explicitly.

### Historical cost

- **Chats / Agents** owns direct project-attributed agent runs.
- Agent runs correlated to a process are excluded because root process records already include process agent/workflow telemetry.
- **Workflows** owns only standalone `ProjectStructureNode` workflow origins.
- `ProcessAssignment` workflow origins are excluded because their usage belongs to the process record.
- **Processes** owns root process records only; child records are excluded to prevent subtree telemetry from being counted twice.
- Actual and estimated values remain distinguishable. Unknown pricing remains visible and is never converted to zero-cost success.

### Future cost

- Future cost means the remaining expected cost for non-terminal canonical project tasks. A task with valid recorded progress contributes `expected cost × (100 − progress) / 100`; an open task without valid progress retains its full estimate and is reported with a completeness warning.
- Each expected task cost is assigned to one mutually exclusive bucket: Agent, Workflow, Process, Workforce, External, or Other/unassigned.
- A task with multiple resource groups is Other rather than being duplicated across buckets.
- Organization and organization-unit assignments are External; person assignments are Workforce.
- Costs retain their currency. USD history and USD future may be shown together; non-USD planned costs remain separate and are never converted implicitly.
- External actual expenses have no canonical ledger today. The report states that limitation instead of fabricating a value.

### Activity

- The main snapshot requests only the latest bounded items and aggregate projections.
- The activity dialog performs its own paged query only after it opens.
- Conversations use the persisted typed project scope first. A centralized compatibility parser recognizes only the supported legacy source formats; malformed attribution is excluded and reported rather than guessed.
- Conversation reporting consistently uses terminal completion time when present and otherwise the latest update time.
- Activity rows expose one canonical activity timestamp rather than describing active rows as completed.
- Persisted run title, result/input summary, typed outcome, and source tags are used. Narrative generation is never performed by a read query.

## State and lifecycle

`Tabs` uses server rendering, and the router does not keep routed components alive. Component fields alone cannot satisfy state retention.

`ProjectManagerSummaryStateStore` is scoped to the interactive application session and keyed by the active database-profile ID plus project ID. It retains:

- selected range, content mode, and scope;
- the last resolved project-scope fingerprint;
- the last successful bounded summary snapshot;
- the load timestamp.

Loading uses a new cancellation token. A failed refresh keeps the prior successful snapshot visible with an explicit error; it does not silently substitute stale data as if it were current.

The detail dialog state is not retained after it closes because its pages can be large and are intentionally loaded on demand.

## Pattern selection record

### Context and forces

- Three persistence systems expose different optimized read models.
- The page is already a monolith and must not absorb reporting logic.
- The result needs deterministic cost ownership and a test seam.
- There is one report composition use case, not a family of interchangeable algorithms.

### Selected structure

No formal design pattern is introduced. A simple extracted application query, pure aggregation helpers, a dedicated Razor component, and a keyed state object are sufficient.

### Rejected alternatives

- Another `ProjectStructurePage` partial: increases an existing hotspot and creates fake modularity.
- A generic repository or provider strategy hierarchy: adds indirection without runtime implementation selection.
- A cross-database reporting entity: creates synchronization and migration ownership before the read model proves it is necessary.
- Querying serialized JSON or run files during every load: prevents useful indexes and scales with total history.
- Loading all activity into the dialog and paging in memory: is not real paging.
- Treating unknown cost as `Other` historical spend: hides missing data.

### Test seam

- Pure calculator tests prove category ownership, currency separation, history/future separation, date bucketing, merged latest activity, and double-count prevention.
- Projection and scope/query tests prove project IDs, time windows, root/standalone filters, bounds, cancellation, and invalid-input rejection.
- Component tests prove no automatic load, retained state across tab disposal, recursive warning confirmation, and dialog creation only after the explicit open action. Projection tests prove the dialog queries bounded server-side pages and filtered aggregates.

## Performance contract

- History-only plan reads project only the canonical task schedule. History-plus-future adds bounded pricing and resource-binding projections without loading dependency/cycle/preview graphs.
- Scope preflight is content-specific, and the subsequent manager query uses `Take(limit + 1)` validation rather than repeating count queries.
- Project/task/assignment reads use set-based project-ID filters and `AsNoTracking`.
- Agent reports query a versioned lightweight index. A legacy index is materialized with bounded concurrency outside exclusive locks, then compare-and-published with one atomic write.
- Workflow origins are denormalized into typed indexed columns; normal queries do not parse `OriginJson`.
- Process analytics use the existing SQL aggregate projection with plural project scope and root-only filtering.
- Independent historical sources may execute concurrently after scope validation.
- Recent lists are bounded to five rows per source before merge.
- Dialog page size is bounded and validated.
- Expense trends are grouped by day in projections and are not built from unbounded detail objects.

## Validation gates

1. Focused unit tests for plan aggregation and all three reporting projections.
2. Focused bUnit tests for the Manager Summary panel and Project Structure tab lifecycle.
3. PostgreSQL persistence tests for workflow-origin and process project/root filters.
4. Full solution rebuild and non-live test gate.
5. Managed `dotnet watch` start on port 5032, readiness check, browser interaction, and console check.

Current focused evidence: 88 Manager Summary/reporting unit tests and 11 Project Structure lifecycle component tests pass; the full solution builds with zero warnings and errors; EF reports no pending model changes after the workflow reporting migration.
