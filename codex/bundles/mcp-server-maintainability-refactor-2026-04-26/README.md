# MCP Server Maintainability Refactor

This bundle coordinates a focused maintainability refactor across the CanDoItAll MCP server family.

## Profile

- `initiative`

## Mission

Refactor the MCP server implementations so shared host concerns live in shared helpers, large files are split around coherent responsibilities, and test seams improve without removing or changing any tool function contracts.

## Bundle Layout

- `inputs/` preserves the raw request and normalized source material.
- `analysis/` records current-state findings, assumptions, risks, and reopen triggers.
- `requirements/` turns the request into observable refactor requirements.
- `architecture/` describes target ownership and isolation boundaries.
- `inventories/` captures the MCP project, file-size, and test-surface inventory.
- `plan/` defines execution order, dependencies, critical foundations, and gates.
- `traceability/` maps every requirement to subbundles and proof.
- `shared-prompts/` keeps implementation and QA prompts reusable.
- `subbundles/` contains execution-ready workstreams.
- `reviews/` captures gate results, validation evidence, and closure.
- `templates/` keeps the local subbundle template from the scaffold.

## Recommended Execution Order

1. `subbundles/01-01-shared-mcp-host-bootstrap`
2. `subbundles/02-02-components-catalog-split-and-tests`
3. `subbundles/03-03-dotnetwatch-host-route-split`
4. `subbundles/04-04-validation-and-closure-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- Do not start file-splitting subbundles until the shared host helper foundation builds and its targeted tests pass.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A - non-UI refactor reviewed`
