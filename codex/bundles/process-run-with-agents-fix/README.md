# Process Run With Agents Fix

This bundle is a coordination and execution package for `process-run-with-agents-fix`.

## Profile

- `initiative`

## Mission

Make the process execution core capable of running a deterministic multi-role calculator delivery process end to end through settings-gated mock agents, including QA rejection, developer repair, QA approval, release handoff, durable artifacts, branch routing, retries, and clear failure diagnostics. This bundle is analysis and planning only; implementation starts after this bundle is accepted.

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
- `inventories/` process/template inventories
- `evidence/` analysis-time test results

## Recommended Execution Order

1. `subbundles/01-01-runtime-lifecycle-and-test-stability`
2. `subbundles/02-02-process-template-qa-repair-model`
3. `subbundles/03-03-mock-agent-staffing-alignment`
4. `subbundles/04-04-dispatcher-completion-contract`
5. `subbundles/05-05-e2e-regression-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Ready for implementation`
- Execution status: `Not started`
- Subbundle gate review: `Prepared`
- Final closure gate: `Not started`
- Browser validation analytics: `N/A for planned backend runtime work unless implementation touches the Process Workspace UI`
