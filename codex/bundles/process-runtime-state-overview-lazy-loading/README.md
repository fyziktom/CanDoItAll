# Process Runtime State Overview And Lazy Loading

This bundle is a coordination and execution package for `process-runtime-state-overview-lazy-loading`.

## Profile

- `initiative`

## Mission

Make the processes page report runtime state truthfully and load runtime detail data only when the UI needs it. The page must stop counting blocked runs as active, must surface active, blocked, and failed run counts through a generic process runtime state projection service, and must let an operator stop blocked runs from the selected process Runs tab without creating a second source of truth.

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

1. `subbundles/01-runtime-state-overview-service`
2. `subbundles/02-lazy-run-detail-loading`
3. `subbundles/03-blocked-run-stop-action`
4. `subbundles/04-validation-and-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed with browser-runtime caveat`
- Browser validation analytics: `Blocked by stale running web app; tests/build passed against isolated artifacts`
