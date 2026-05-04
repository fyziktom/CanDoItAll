# Agent Tab Cards And Dialog Editor

This bundle coordinates the Agents tab layout change requested on 2026-05-04.

## Profile

- `feedback`

## Mission

- Turn the Agents tab into a card-led technical-agent surface that shares the same agent-card component used by the chat switch-agent modal. Double-clicking an agent card opens a DialogService modal with the current technical editor split into tabs, including capability assignment for skills and MCP servers, while preserving save/delete behavior and making long text fields use the available width and height.

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

1. `subbundles/01-shared-agent-card-foundation`
2. `subbundles/02-agents-tab-dialog-editor`
3. `subbundles/03-validation-and-closure`

## Dependency And Validation Map

- The mermaid dependency map, critical-subbundle notes, and phase gates are maintained in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed prepared validator on 2026-05-04`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed after completed validator on 2026-05-04`
- Browser validation analytics: `Completed`
