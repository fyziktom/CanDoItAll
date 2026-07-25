# optimized-history-detail-and-api-read-paths

## Status

- `Completed`

## Objective

- Expose bounded typed record APIs and move normal historical Runs/Graphs/Analytics/manager/dashboard/cost consumers to compact records while keeping explicit deep evidence routes.

## Success Criteria

- `GET /api/processes/runs` returns a stable bounded cursor page with indexed filters.
- `GET /api/processes/runs/{runId}/summary` and `/analytics` use one compact record and expose freshness/completeness/narrative state.
- Graph data is included in summary or an explicit record-backed graph route based on the final contract.
- Ordinary GET routes do not invoke global projection catch-up.
- Normal historical consumers load zero runtime aggregate, assignment, or Agent Framework detail rows per result.
- Existing deep run/history diagnostics remain explicit and compatible.

## Covered Inputs

- R07-R10, R13; N001, N002, N006-N009.

## Prerequisites

- SB03 progression gate and Architecture A2 pass.

## Exact Source References

- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\ProcessRunRecordsApi.cs`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Application\ProcessRuntimeProjectionQueryService.cs`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Application\ProcessWorkspaceShellProjectionService.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceShell.razor`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\Components\LiveProcessesDashboard.razor`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Application\ProcessDashboardActivityQueryService.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Services\Hr\HrAgentProcessReviewService.cs`

## UI Composition Contract

- Preserve existing large-screen Runs/Graphs/Analytics surfaces and scroll ownership.
- No new dashboard/cards/dialogs are required; bind existing stats/graphs to record DTOs.

## Deliverables

- Application query service and versioned public API DTOs.
- List, summary, analytics, and record-backed graph behavior.
- DB-side filters for project/definition/disposition/agent/date and bounded cursor paging.
- Removal of foreground catch-up from ordinary GETs with explicit freshness.
- Summary-only/batch observation/usage reader option for remaining live/drill-down paths.
- Targeted consumer migrations and compatibility tests.

## Architecture Impact

- Web performs validation/mapping only.
- Existing large query service receives only minimal removal/delegation edits; new record use cases remain cohesive top-level types.
- Canonical evidence routes remain intentionally named and opt-in.

## Dependency Impact

- SB05 documents the finalized API. SB06 measures and gates the outcome.

## Validation Depth

- Proof tier: `Behavioral`.

## Implementation Steps

1. Add validated API query/response contracts and map list/summary/analytics routes.
2. Prove filters/limits/cursors execute in persistence.
3. Remove foreground catch-up from ordinary reads and expose projection/record freshness.
4. Bind terminal workspace/dashboard/cost/manager/CRM seams to records where in scope.
5. Make Agent Framework detail optional and batch header lookup for remaining explicit live detail.
6. Add throwing-fake, EF command-budget, 500+ record, cursor, not-found, and compatibility tests.
7. Run Architecture Checkpoint A3.

## Scope Exceptions

- A general redesign of active-run operational enrichment and the entire file workspace index is deferred; explicit active/deep detail may retain canonical reads.
- UI redesign is not authorized.

## Do Not Do

- Do not use `Skip` for unbounded historical pagination.
- Do not filter scalar query keys by deserializing JSON in memory.
- Do not add `Task.WhenAll` over the shared scoped `DbContext`.

## Acceptance Checklist

- [x] API contracts are typed, bounded, cancellable, and tested.
- [x] No normal historic per-row deep hydration.
- [x] Foreground global catch-up is absent from GETs.
- [x] Graph/analytics use stored aggregates.
- [x] Existing explicit diagnostics remain compatible.
- [x] Architecture A3 passes.

## Proof Required

- Focused unit/integration/API tests including query/call budgets.
- Affected builds and route inspection.
- If rendered markup changes, Playwright at 1600x900 with Runs/Graphs/Analytics assertions and screenshot review.

## Browser Validation Logging

- N/A: no rendered markup, CSS, route layout, dialog, or scroll-owner code changed. Data-source behavior is covered by query, API serialization, workspace, dashboard, cost, and project-node tests.

## Actual Proof And Progression

- Entry and closure gates: `Pass`.
- `ProcessRunRecordQueryServiceTests` proves typed filters, opaque keyset cursors, bounds, one-record graph derivation, and no mutation calls.
- API serialization tests prove compact list shape, independently paged steps/minute buckets, page-local graph edges, analytics denominators/data watermarks, predictable validation/not-found, and exclusion of generated result/event payload details.
- Workspace tests prove explicit historic selection, zero deep rebuild for a terminal record, and exact durable event aggregates. Dashboard tests prove one exact-key projection batch and compact record fallback. Historic cost tests prove root-only record aggregation.
- `ProcessesApi` contains no foreground `ProcessRuntimeProjectionCatchupService` calls; background replay owns catch-up.
- The HTTP integration host compiled. Live route execution was attempted but Docker/PostgreSQL provisioning was unavailable; this is recorded as an environment-limited proof gap, not a pass.
- Progression decision: `Completed on deterministic source/unit/serialization/integration-build evidence; SB05 documents only the compiled contract and the live-environment limitation.`

## Behavioral Semantic Adequacy

- Raw note owned: `N001`, `N002`, `N006`, `N007`, `N008`, and `N009`: cheap expanding history, architectural reuse, Runs/Graphs/Analytics and other consumers, async/deep-load discipline, Processes API exposure, and modularity.
- Shipped behavior: bounded compact list, per-run summary/graph, scalar analytics, opaque keyset cursors, honest record/source watermarks, and record-backed workspace/dashboard/cost/project consumers are available without foreground projection replay.
- Source proof: `ProcessRunRecordQueryService.cs`, `ProcessRunRecordsApi.cs`, `ProcessWorkspaceShellProjectionService.cs`, `ProcessDashboardActivityQueryService.cs`, `EfProcessHistoricalRunCostReader.cs`, and the narrow `IProcessRunRecordReader`.
- Test proof: query-service tests cover filters/cursors/bounds/graph derivation; API tests cover safe serialized shapes, independent subpaging, analytics and errors; workspace, dashboard, historic-cost, and project-structure tests cover their production record-backed paths.
- Shallow-pass trap: a bounded-looking endpoint that first hydrates all canonical rows, filters JSON in memory, invokes foreground catch-up, or returns a compact DTO after loading full Agent Framework details would not solve the historical-read problem.
- Adversarial negative proof: invalid bounds/cursors/date windows never reach the store; missing records return predictable not-found; compact list data excludes narrative/full fact bodies; terminal workspace reads refuse deep rebuild; dashboard uses one bounded exact-key batch; cost reads do not load runtime telemetry.
- Semantic positive proof: filters and cursors round-trip through typed store queries, graph edges derive from stored dependencies, analytics expose correct available/unavailable denominators and data watermarks, and each migrated consumer returns the intended durable terminal result.
- Anti-stub audit: tests call the real application query/mapping and consumer services with throwing/counting boundaries, while route source contains no foreground catch-up; two HTTP contract tests execute the real mapped endpoints on an in-memory host with the record store replaced at the service boundary, while unavailable PostgreSQL-backed proof remains explicit.

## Progression Gate

- SB05 starts only after finalized route/payload behavior and API tests pass; SB06 requires performance budgets to pass.

## Reopen Triggers

- Documentation discovers unstable/ambiguous route semantics; a normal consumer still performs deep hydration; cursor duplicates/skips under concurrent inserts.

## Suggested Agent Prompt

```text
Implement SB04 only. Make record-backed reads bounded and explicit, preserve deep diagnostics as opt-in, and prove store/detail call budgets before changing documentation.
```
