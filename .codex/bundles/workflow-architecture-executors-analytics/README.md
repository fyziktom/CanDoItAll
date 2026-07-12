# Workflow Architecture, Executors, Analytics, and Large-Screen UI

This initiative bundle coordinates the workflow architecture and implementation work requested on 2026-07-12.

## Profile

- `initiative`

## Mission

- Deliver a testable workflow platform in which contracts point inward, every executable node has one authoritative descriptor, tools and workflow executors share typed application operations, all supported launch paths behave consistently, workflow cost and token analytics are queryable, and the large-screen editor renders built-in and trusted plugin settings without hard-coded executor branches.

## Outcome Contract

- Requested outcome: improve workflow architecture, executor coverage, lifecycle integration, analytics, tests, and large-screen workflow UI.
- Hard constraints: preserve explicit failures and governance, avoid stringly typed dispatch, do not duplicate tool/executor implementations, and do not spend time on small or medium layouts.
- Evidence required before closure: focused unit/component/integration tests, solution build, dependency-direction assertions, executor catalog/manifest consistency checks, lifecycle tests, analytics arithmetic tests, and a maximized large-screen browser pass.
- Known blockers or explicit scope exceptions: the components MCP transport was unavailable during preparation; retry it before UI edits. Existing cycles inside `CanDoItAll.Modules.AgentFramework` are baseline findings and must not increase.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` current inventory, boundary decisions, target solution, and testability plan
- `plan/` execution order, architecture checkpoints, and dependencies
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` readiness, gate, and execution reports

## Recommended Execution Order

1. `subbundles/01-architecture-contracts-and-composition-foundation`
2. `subbundles/02-shared-tool-operations-and-executor-adapters`
3. `subbundles/03-missing-standard-and-plugin-executor-coverage`
4. `subbundles/04-workflow-lifecycle-entry-point-parity`
5. `subbundles/05-workflow-usage-and-cost-analytics`
6. `subbundles/06-large-screen-workflow-ui-and-extensible-settings-renderers`
7. `subbundles/07-integration-browser-and-architecture-closure`

## Dependency And Validation Map

- `plan/01-phase-plan.md` is authoritative for dependencies and critical gates.
- Resume from this README, the current subbundle README, and `reviews/01-execution-report.md` after compaction or handoff.
- A failed progression gate reopens the owning subbundle; downstream work does not patch around it.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed; SB01-SB07 progression gates passed`
- Subbundle gate review: `Passed; every subbundle is completed with artifact-backed proof`
- Final closure gate: `Passed; solution/scoped validation, EF convergence, architecture, browser, and completed-stage validator are green`
- Browser validation analytics: `Passed at 1600x1000 on /agents/workflows; four reviewed desktop captures are recorded in SB06 proof`
