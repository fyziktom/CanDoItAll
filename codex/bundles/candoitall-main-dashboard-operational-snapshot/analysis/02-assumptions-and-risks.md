# Assumptions And Risks

## Assumptions

- Active workflow states are exactly `Running` and `WaitingForInput`; terminal, idle, and not-started states appear only in the latest-five fallback.
- Active process selection uses canonical `ProcessRuntimeStatus` policy and deterministic runtime `UpdatedAtUtc`/run-ID order; projection status is display evidence only.
- The typed cache identity uses `IDatabaseRuntimeState.GetSnapshot()` and rejects a missing profile identity; fingerprint is compared but never logged.
- Dashboard rows and usage totals are global operational data, so one app-process singleton cache is intentionally shared across Blazor circuits. No user identity, authorization decision, or per-circuit state may enter that cache.
- Each actual refresh goes through singleton `IDashboardSnapshotLoadRunner`, which creates/disposes a fresh async DI scope and resolves scoped `IDashboardSnapshotLoader` there. `IServiceProvider` resolution is confined to this lifetime adapter.
- The cached project/workflow/process collection graph uses `ImmutableArray<T>` and the loader fails explicitly if any source returns more than five rows.
- Process active/recent selection comes from canonical runtime state. Projection reads are limited to the selected five run IDs and projection lag is surfaced rather than treated as canonical activity state.
- `KnownCostUsd` is a known-observation subtotal and must be labelled “Known cost (USD).”
- Existing page/body scroll behavior remains the sole scroll owner; Home does not modify the app shell.

## Critical Path Risks

- SB01 is critical. Wrong active/fallback semantics, cache identity, or coalescing makes every SB02 visual assertion misleading.
- A full overview/list call hidden inside a “thin” query can pass row-count tests while retaining the performance defect. Source assertions and counting fakes/interceptors are mandatory.
- Holding a lock across asynchronous I/O, retaining a scoped loader/scope/caller token in singleton state after refresh completion, reusing a scope across refreshes, or caching a faulted task can deadlock, dispose live dependencies, or poison all circuits.
- A failed automatic refresh must advance the next eligible attempt by five minutes; otherwise every visiting circuit can create a database retry storm.
- Parallel source loading can expose unsafe shared-context use. Each query owns its safe context/store boundary; if that cannot be proven, the loader must serialize only the unsafe source rather than use service location.
- Projection freshness may lag canonical runtime state. The UI must surface lag and cannot relabel projection data as canonical activity.
- Provider evidence now authorizes only the three dashboard indexes and workflow `UNION ALL` active-or-fallback query recorded in `bundle://evidence/SB01/postgresql-dashboard-index-plans.md`. Extra status-leading indexes or unrelated migration churn remain out of scope.

## Validation Risks

- CodeAnalytics is unavailable; automated dependency/cycle and hotspot evidence cannot be produced during preparation.
- A five-minute browser wait is wasteful and flaky. Fake `TimeProvider` behavioral tests prove expiry/automatic refresh; Playwright proves a decreasing countdown and manual reset at real time.
- Browser seed state may lack both active and fallback examples. Playwright fixtures must seed deterministic recent/active rows or explicitly split scenarios.
- Screenshot-only proof can miss duplicate loader calls, hidden enrichment, and silent fallback. These require direct tests/source assertions.
- The exact first viewport depends on shell chrome. Browser proof records `window.innerWidth/innerHeight`, document scroll dimensions, dashboard bounds, and first three row visibility rather than relying on a visual impression.

## Reopen Triggers

- Reopen SB01 and invalidate SB02/SB03 data proof if any query returns more than five rows, lacks deterministic order/`AsNoTracking`, calls a broad overview/list path, infers process activity from projections, hides projection lag, or treats mixed active/terminal data as a combined list.
- Reopen SB01 if concurrent calls invoke the loader more than once, force refresh bypasses an in-flight task, caller cancellation cancels shared work, failure is not throttled for five minutes, a faulted task is retained, a fresh async scope is not used for every actual refresh, or a runtime identity change reuses/publishes the prior result.
- Reopen SB01 if Home, a query, the loader, or cache policy injects/resolves `IServiceProvider`, or if the loader injects `ISandboxWorkspaceStore`, an EF context, `ProjectsService`, or `IAgentFrameworkWorkspaceService`. Provider resolution is allowed only in the typed singleton lifetime adapter.
- Reopen SB01 if any user-specific/authorization-sensitive value enters the shared cache, any cached collection is mutable, or the loader accepts more than five project/workflow/process rows.
- Reopen SB02 if `QuickActionCard` is page-local, quick-action labels/routes/count differ, icons are not centered in square cards, Home owns data policy, or errors become empty states.
- Reopen SB02 if timer work survives disposal, refreshes overlap, countdown is inaccessible/large, a feature overlay appears, nested scrolling is introduced, or first-viewport targets fail at `1440x900`.
- Reopen the responsible subbundle when SB03 architecture, performance, build, test, or browser evidence fails. Do not list a user-visible proof gap as residual risk.
- If an older active canonical process is omitted or projection lag is reported as fresh, reopen SB01; do not compensate with an unbounded projection scan or repeated catch-up loop.
