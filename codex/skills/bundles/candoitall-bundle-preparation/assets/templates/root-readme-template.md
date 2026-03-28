# {{BUNDLE_TITLE}}

This bundle is a coordination and execution package for `{{BUNDLE_NAME}}`.

## Profile

- `{{PROFILE_NAME}}`

## Mission

- Describe the desired end state in one short paragraph.

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

1. `subbundles/01-...`
2. `subbundles/02-...`
3. Continue until the final validation subbundle is complete.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle readiness gate: `Not run`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not run`
- Browser validation analytics: `Not started`
