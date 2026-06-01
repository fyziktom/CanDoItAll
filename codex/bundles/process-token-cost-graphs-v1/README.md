# Process Token Cost And Graph Analytics

This bundle is a coordination and execution package for `process-token-cost-graphs-v1`.

## Profile

- `initiative`

## Mission

- Make process token usage, cached-token accounting, pricing, and analytics graphs reflect provider-reported usage accurately. Add lazy-loaded process and process-run graph views to the process workspace without making accidental tab clicks trigger large historical queries.

## Outcome Contract

- Requested outcome: process execution metrics count input, cached input, output, and tool-call usage correctly for OpenAI/Azure OpenAI and non-cached providers; live and historical process analytics show price graphs after runs finish; selected process and selected run details expose graph tabs with explicit lazy loading.
- Hard constraints: preserve strongly typed usage fields, avoid stringly typed provider accounting, do not silently invent fallback pricing, keep Blazor UI inside existing ProcessWorkspace and LiveProcessesDashboard patterns, and defer historical graph loading until the user explicitly requests all-runs graphs.
- Evidence required before closure: targeted unit/integration/component tests for token aggregation, pricing, history analytics, and lazy graph tabs; build result; browser proof for the new graph tabs and live dashboard history graph behavior.
- Known blockers or explicit scope exceptions: no external OpenAI billing API reconciliation is in scope; validation uses local provider-response fixtures and persisted execution metrics.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-token-usage-cost-accounting`
2. `subbundles/02-02-history-analytics-data`
3. `subbundles/03-03-process-workspace-graph-tabs`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Implemented; browser validated with disposable PostgreSQL profile`
- Subbundle gate review: `SB01-SB03 implemented with targeted test proof`
- Final closure gate: `Completed-stage validator passed`
- Browser validation analytics: `Passed on 2026-06-01 against localhost:5034 disposable PostgreSQL database; default local profile still has baseline mismatch`
