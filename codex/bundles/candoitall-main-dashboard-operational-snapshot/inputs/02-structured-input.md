# Structured Input

## Core Objective

Provide a fast, honest, desktop-first Home operational snapshot that gives users direct navigation and the latest project/runtime context without making the Razor page own data policy, caching, persistence, or aggregation.

## Success Criteria

- `/` and `/dashboard` render exactly four square, centered-icon quick actions: Projects, Agents, Live Processes, Scheduler.
- The snapshot exposes at most five recent projects; active workflow runs or, only when none are active, the latest five; and the same active-first rule for process runs.
- Agent Overview `TotalTokens`, `KnownCostUsd`, and source `UpdatedAtUtc` come from the existing usage projection through a narrow typed query.
- An app-process singleton typed service/cache shares one coherent global operational snapshot across Blazor circuits for five minutes, coalesces concurrent refreshes, varies by current database runtime profile ID/fingerprint/generation, supports force refresh, and never turns a load error into empty data. Every actual refresh uses a typed singleton runner that creates a fresh async DI scope for the scoped loader.
- While Home remains open, a tiny countdown advances, expiry triggers refresh, manual refresh bypasses a fresh cache, and disposal stops the timer.
- At `1440x900`, the compact header, four actions, main section headers, and useful list rows are visible before page scrolling; the existing page/body remains the sole scroll owner.
- All three Behavioral subbundles satisfy realistic positive and adversarial negative proof before final closure.

## Hard Constraints

- Exactly three subbundles; do not add a fourth catch-all or migration phase.
- No new project references, partial class files, general service locator calls, new package/assets, direct JavaScript, custom app shell, or mobile/tablet work. Resolving the scoped loader through `IServiceScopeFactory`/`IServiceProvider` is permitted only inside the typed Web composition/lifetime runner.
- Do not extend or invoke the aggregate workflow overview for dashboard activity.
- Do not call `ProjectsService.ListAsync`, `IAgentFrameworkWorkspaceService.GetAgentOverviewAsync`, or runtime-enriched process queries from the dashboard loader.
- All persistence queries use typed projections, deterministic order, `AsNoTracking` where EF is involved, and a hard upper bound of five. The cached project/workflow/process graph uses `ImmutableArray<T>` and the loader rejects any source that violates the bound.
- No workflow/process/agent token or cost totals are summed together; the two header metrics are AgentFramework usage projection totals only and cost is labelled as known cost.
- Home owns rendering, UI state, timer lifecycle, and orchestration only. `QuickActionCard` belongs to `CanDoItAll.AppComponents`.
- Errors are explicit and logged with actionable non-secret state; stale display after refresh failure must be visibly marked.

## Allowed Side Effects

- Production/test edits described by SB01 and SB02 after their entry gates pass.
- SB03 may add proof-only tests/artifacts or reopen the owning subbundle; it must not silently expand feature scope.
- Existing package/project reference graphs, shell layout, global styles/assets, and non-dashboard routes remain unchanged.

## Source Artifacts

- `bundle://inputs/00-original-request.md`
- `bundle://inputs/01-source-artifacts.md`
- `bundle://analysis/01-current-state.md`
- `bundle://inventories/01-scope-inventory.md`

## Input Coverage Signals

| Note | Literal scope signal |
| --- | --- |
| `N001` | “quick actions Projects/Agents/Live Processes/Scheduler as compact square centered-icon cards” |
| `N002` | “latest 5 projects” |
| `N003` | “active workflow runs or fallback latest 5” plus `O001` dedicated-query refinement |
| `N004` | “active process runs or fallback latest 5 in shared tabs” |
| `N005` | “Agent Overview TotalTokens and KnownCostUsd” plus `O002` narrow-query refinement |
| `N006` | “five-minute snapshot cache”; the resolved architecture is one app-process singleton cache shared across circuits because the data is global and navigation must not reload it |
| `N007` | “automatic refresh while open, forced refresh, tiny countdown” |
| `N008` | “new app-level typed DashboardSnapshotService caching a thin IDashboardSnapshotLoader”; a typed singleton runner bridges to a fresh scoped loader per actual refresh |
| `N009` | “no new project refs/partial classes/service locator”; only the explicit composition/lifetime adapter may resolve the scoped loader |
| `N010` | “cache key includes runtime database profile generation”; resolved identity is profile ID, fingerprint, and generation |
| `N011` | “explicit errors, no silent fallback” |
| `N012` | “quick action wrapper belongs in AppComponents; Home remains rendering/orchestration only” |
| `N013` | “queries bounded, AsNoTracking, process snapshot-only/no enrichment, once per 5m, concurrent refresh coalescing” |
| `N014` | “exactly 3 subbundles” with all three `Behavioral` and SB01 critical |
| `N015` | required C# architecture artifacts and gates |
| `N016` | literal raw input and portable `repo://` / `bundle://` references |
| `N017` | `1440x900` first-viewport/page-scroll composition and no overlays |
| `N018` | exact proof, shallow traps, negative proof, traceability, reopen rules, execution tables, honest states |
| `N019` | “CodeAnalytics unavailable” evidence limitation |

## Dependency And Sequencing Signals

- SB01 is the critical foundation: it freezes DTOs, bounds, source policies, cache behavior, and DI seams. SB02 must not invent around an incomplete snapshot contract.
- SB02 owns all feature-visible markup/state and depends on SB01's tested error/refresh semantics.
- SB03 independently evaluates both. A data/cache failure reopens SB01; a layout/timer/accessibility failure reopens SB02; dependent proof is then invalid.

## Validation Expectations

- SB01: isolated cache/coalescing/time/failure tests plus query service/store positive and adversarial tests using more than five rows and mixed active/terminal states.
- SB02: bUnit tests for the wrapper, exactly four routes, loading/populated/empty/error/stale states, shared tabs, countdown, force refresh, automatic expiry, and disposal.
- SB03: solution build, all targeted suites, source/dependency assertions, architecture gate, and Playwright at `1440x900`.
- Structure-only checks do not close any subbundle. Each must fill the semantic evidence fields in `bundle://reviews/01-execution-report.md`.

## Evidence Contract

- Prepared structure: `python C:/Users/lucys/.codex/skills/candoitall-bundle-preparation/scripts/validate_bundle.py codex/bundles/candoitall-main-dashboard-operational-snapshot --profile initiative --stage prepared --repo-root .`
- Build: `dotnet build CanDoItAll.slnx --no-restore -nologo -v:minimal`
- Targeted tests and filters are frozen in each subbundle, not replaced by “tests pass.”
- Browser screenshot targets: `bundle://evidence/SB03/home-dashboard-1440x900-populated.png`, `...-empty.png`, `...-refresh-error.png`, and `...-page-scroll.png`.
- Behavioral proof must record raw note, shipped behavior, source proof, test proof, shallow-pass trap, adversarial negative, semantic positive, and anti-stub audit.

## UI Validation Strategy

- Primary surface: recent projects and operational activity list.
- Supporting content: Quick actions are a compact lead row; `TotalTokens`, `KnownCostUsd`, refresh action, and countdown stay in the compact PageHeader.
- Stats treatment: `CompactStatStrip`/`CompactStat`; no metric-card grid and no repeated totals.
- List/editor organization: recent projects remain a bounded list; workflow/process views use `Tabs`/`TabsItem`; there is no editor, dialog, textarea, or feature overlay.
- First viewport: at `1440x900`, show the compact header, four quick actions, both primary section headings, and at least three rows in each visible collection.
- Scroll owner: existing routed-page/body scrolling reveals remaining rows; tab panels and lists do not create nested scrolling.

## Browser Validation Analytics

Every SB02/SB03 browser row records route, `1440x900` viewport, seeded scenario, actions/assertions, screenshot path, first-viewport result, vertical/lateral overflow measurements, scroll owner, and confirmation that no feature overlay appeared. Small/medium viewports are intentionally omitted.

## Working Assumptions

- `IDatabaseRuntimeState.GetSnapshot()` supplies the authoritative profile ID, fingerprint, and generation cache identity inside one app process; a missing identity fails explicitly and fingerprints are never logged.
- Dashboard snapshot data is global operational data. No user-specific, authorization-sensitive, or per-circuit UI data may enter the singleton cache.
- “Active workflow” means `WorkflowRunState.Running` or `WorkflowRunState.WaitingForInput`.
- “Active process” follows canonical typed `ProcessRuntimeStatus` terminal-state policy; projection status is display evidence only and projection lag remains visible.
- A prior snapshot may remain rendered after a refresh error only when an explicit error/stale state is visible and retry remains available.
- Existing Web project references are sufficient; any discovered missing reference triggers architecture repair rather than adding one casually.

## Primary Risks

- The current process snapshot store filters active status only after JSON deserialization and reads a bounded latest candidate window; extreme projection volume can hide older active rows.
- PostgreSQL TEMP-table evidence at 200,000 rows justified the ordered project/process indexes and showed the workflow state composite helps only with the active-or-fallback `UNION ALL` query shape. The narrow migration/query implementation and final targeted Behavioral gate completed SB01; independent final performance proof still belongs to SB03. See `bundle://evidence/SB01/postgresql-dashboard-index-plans.md`.
- Blazor timer disposal and late completion can mutate a disposed component unless cancellation and post-await guards are explicit.
- Force refresh can accidentally create duplicate loads unless it joins an existing in-flight task.
- Unknown-cost usage observations are excluded from `KnownCostUsd`; the label must not imply total spend.
