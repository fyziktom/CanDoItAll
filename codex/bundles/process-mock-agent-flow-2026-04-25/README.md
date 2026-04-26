# Deterministic Process Mock Agents

This bundle is a coordination and execution package for `process-mock-agent-flow-2026-04-25`.

## Profile

- `initiative`

## Mission

- Add a deterministic, settings-gated mock agent runtime for process automation tests. The mock agents must exercise a multi-role calculator delivery flow, write handoff artifacts through the AgentFramework workspace pipeline, and force a developer to QA to repair to QA approval iteration without calling real LLM agents.

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

1. `subbundles/01-01-architecture-map-and-mock-seam`
2. `subbundles/02-02-settings-gated-mock-agent-runtime`
3. `subbundles/03-03-calculator-process-script-and-qa-repair-loop`
4. `subbundles/04-04-targeted-validation-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `N/A backend slice`
