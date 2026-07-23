# CanDoItAll Main Dashboard Operational Snapshot

Initiative bundle for replacing the current workbench-oriented Home page with a compact operational snapshot. Implementation and Behavioral proof are complete across all three subbundles.

## Profile

- Content profile: `initiative`
- Proof tiers: SB01 `Behavioral`, SB02 `Behavioral`, SB03 `Behavioral`
- Bundle root: `bundle://`
- Repository root: `repo://`

## Mission

Make `/` and `/dashboard` useful at a glance: four direct operational destinations, five recent projects, active-first workflow and process activity, and authoritative AgentFramework token/cost totals, all served through one typed five-minute snapshot with honest refresh state.

## Outcome Contract

- Requested outcome: a compact Home dashboard with Projects, Agents, Live Processes, and Scheduler quick actions; operational lists; Agent Overview totals; automatic and forced refresh; and a tiny expiry countdown.
- Hard constraints: Home renders and orchestrates only; `QuickActionCard` belongs to `CanDoItAll.AppComponents`; data access remains behind typed module/application queries; no new project references, partial classes, service location outside the dedicated Web lifetime adapter, silent fallback, unbounded query, or process enrichment is allowed.
- Evidence required before closure: exact targeted tests, solution build, behavioral positive and adversarial negative proof, C# architecture gate, deterministic component proof for loading/empty/error/stale/timer states, and inspected `1440x900` Playwright evidence for the representative populated flow, countdown, force-refresh, tabs, route alias, and page-scroll ownership.
- Explicit scope exceptions: no mobile/tablet tuning, charts, dialogs, menus, floating surfaces, new package references, or summing of agent/workflow/process usage totals.
- Known preparation gap: CodeAnalytics MCP is unavailable. Direct source inspection anchors this bundle; SB03 must retry CodeAnalytics and otherwise record manual project-reference/cycle evidence before closure.

## Frozen Architecture Decisions

1. DI-managed singleton `DashboardSnapshotService` and `DashboardSnapshotCache` share global operational snapshots and in-flight refreshes across Blazor circuits. Each actual refresh calls singleton `IDashboardSnapshotLoadRunner`, which creates a fresh async scope, resolves scoped `IDashboardSnapshotLoader`, awaits it, and disposes the scope. Provider resolution exists only in that composition/lifetime adapter; no user-specific data is cached.
2. The cache key is the typed `DatabaseRuntimeSnapshot` identity (profile ID, fingerprint, and generation); automatic eligibility is exactly five minutes after the last successful capture or failed attempt.
3. Projects, workflow activity, process activity, and agent usage each expose narrow typed queries. The loader does not inject EF contexts, stores, `IServiceProvider`, `ProjectsService`, or the full Agent Overview service. Its cached collection graph is `ImmutableArray<T>` and rejects more than five rows per bounded source.
4. Workflow activity uses a dedicated bounded activity store/query (`Running` and `WaitingForInput`, otherwise latest five). It does not extend or execute the aggregate `WorkflowOverviewSnapshot` path.
5. Agent totals use `IAgentUsageTotalsQueryService` over `ISandboxWorkspaceStore.LoadUsageProjectionAsync`; the dashboard loader never sees the file-backed store.
6. Process activity selects active-or-recent run IDs from canonical runtime state, then may enrich only those five rows from projections while surfacing projection freshness; it never infers active state from the bounded projection window.
7. Failures remain failures. A prior successful snapshot may remain visible only with an explicit refresh-error/stale indication; no source substitutes empty or unrelated data.

The rationale and rejected alternatives are in `bundle://architecture/03-csharp-pattern-selection-records.md`.

## Bundle Layout

- `inputs/`: literal request, later operator refinements, and normalized input
- `analysis/`: inspected repository state, assumptions, risks, and reopen rules
- `requirements/`: requirement IDs and observable completion conditions
- `inventories/`: source, route, test, component, and dependency inventory
- `architecture/`: target solution plus the C# architecture guard artifacts
- `plan/`: dependency sequence and architecture checkpoints
- `traceability/`: input/requirement-to-owner/proof/closure mapping
- `shared-prompts/`: execution and independent QA handoff prompts
- `subbundles/`: exactly three work units
- `reviews/`: preparation review, execution report, and architecture gate

## Recommended Execution Order

1. `bundle://subbundles/01-dashboard-snapshot-foundation/README.md` — SB01 critical data/cache foundation (`Completed`, AC01 `Approved`).
2. `bundle://subbundles/02-dashboard-quick-actions-and-activity-ui/README.md` — SB02 AppComponents wrapper and Home rendering/orchestration (`Completed`, AC02 `Approved`).
3. `bundle://subbundles/03-performance-architecture-and-browser-closure/README.md` — SB03 independent performance, architecture, browser, and final closure (`Completed`, AC03 `Approved`).

All progression gates passed in order. The documented reopen rules remain binding if later changes invalidate query/cache, UI/timer, or closure proof.

## Dependency And Validation Map

- Execution order and invalidation rules: `bundle://plan/01-phase-plan.md`
- C# checkpoints: `bundle://plan/architecture-checkpoints.md`
- Requirement ownership: `bundle://traceability/01-requirement-traceability.md`
- Durable state and pending proof: `bundle://reviews/01-execution-report.md`

## UI Target Policy

- Target viewport: `1440x900`, maximized desktop, existing app shell.
- Primary surface: recent projects beside the mutually exclusive workflow/process activity tabs.
- Supporting content: compact quick-action lead row and Agent Overview totals in the compact page header.
- First viewport: compact header, all four quick actions, both main section headers, and at least the first three rows of each visible collection. Remaining fourth/fifth rows use the existing routed-page scroll owner.
- Scroll owner: the existing page/body owner only; no nested list or tab-panel scrolling and no lateral overflow.
- Overlays: none are introduced. “Open-overlay” proof is `N/A by explicit scope`; browser proof must still confirm that dashboard interactions open no feature overlay.
- Small/medium/mobile tuning is out of scope because this is an application page, not a reusable BaseLib primitive.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `SB01, SB02, and SB03 completed`
- Subbundle gate review: `AC00 passed; AC01, AC02, and AC03 approved`
- Final closure gate: `Passed — completed-stage validator accepted the bundle on 2026-07-22`
- Browser validation analytics: `Passed — populated, alternate-tab, empty, stale-error, target-viewport, and supplementary mobile screenshots inspected`
- Prepared-stage structural validator: `Passed on 2026-07-22`
- Completed-stage structural validator: `Passed on 2026-07-22`
