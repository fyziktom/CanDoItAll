# Project Structure Node Actions

This bundle coordinates the project-structure node action work requested on 2026-04-30.

## Profile

- `feedback`

## Mission

Project-structure runtime nodes must expose explicit run choices from both the double-click quick-action dialog and the right-click canvas menu, file-backed nodes must expose the correct local or IPFS open action, and the same node capability information must be visible through the Project Structure MCP and internal agent project-structure tools.

## Bundle Layout

- `inputs/` raw request, source inventory, and structured input
- `analysis/` current state, assumptions, risks, critical path, and reopen triggers
- `requirements/` normalized, testable requirements
- `architecture/` target solution and ownership boundaries
- `plan/` execution order, dependency map, critical subbundles, and phase gates
- `traceability/` raw-note and requirement coverage
- `shared-prompts/` implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` self-review and execution report

## Recommended Execution Order

1. `subbundles/01-runtime-node-run-actions`
2. `subbundles/02-file-and-ipfs-open-actions`
3. `subbundles/03-mcp-and-internal-agent-action-contracts`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- Subbundle 01 is the critical UI and host-action foundation for runtime nodes.
- Subbundle 02 depends on the same quick-action and context-action patterns used in subbundle 01.
- Subbundle 03 depends on the final Workbench capability model shipped by subbundles 01 and 02.

## Validation Summary

- Bundle preparation status: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed with documented browser limitation`
- Final closure gate: `Passed`
- Browser validation analytics: `Partial: Project Structure canvas loaded; full runtime/file fixture proof blocked by existing app/runtime health issues`
