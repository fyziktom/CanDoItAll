# Processes MCP UI Repair

This bundle is a coordination and execution package for `processes-mcp-ui-repair-2026-04-09`.

## Profile

- `feedback`

## Mission

- Repair the processes workspace so the global `/processes` page loads definitions on first render and the definition summary cards count roles and steps from one authoritative version only. Keep the Processes MCP transport simple and unchanged: no token wiring, no HTTP bridge work, and no fallback communication paths.

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

1. `subbundles/01-global-processes-page-initial-load-and-profile-coherent-visibility`
2. `subbundles/02-definition-summary-counts-and-verification-closure`
3. Capture build, test, MCP, DB, and browser proof in `reviews/01-execution-report.md`.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed in repo`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed with MCP session restart caveat`
- Browser validation analytics: `Captured`
