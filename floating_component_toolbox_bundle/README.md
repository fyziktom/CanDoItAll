# Generic Floating Component Toolbox

This bundle coordinates the extraction and migration of a generic floating component toolbox for CanDoItAll canvas-like workbenches.

## Profile

- `initiative`

## Mission

- Create a reusable toolbox principle in the proper shared library so project structure, process canvas, prompt factory, and WebGL workbenches can present different component catalogs through the same floating-window behavior while preserving all existing add/create flows.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input.
- `analysis/` current state, assumptions, and risks.
- `requirements/` normalized, testable requirements.
- `architecture/` target solution and important boundaries.
- `plan/` execution order and dependencies.
- `traceability/` requirement-to-bundle mapping.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `reviews/` bundle self-review and execution report.
- `inventories/` affected source and validation inventory.
- `templates/` reusable subbundle template.

## Recommended Execution Order

1. `subbundles/01-01-shared-toolbox-contract`
2. `subbundles/02-02-canvas-host-migration`
3. `subbundles/03-03-webgl-toolbox-authoring`
4. `subbundles/04-04-validation-and-regression-proof`

## Dependency And Validation Map

- The shared OverlayLib toolbox contract is the critical foundation.
- Canvas hosts migrate through adapters and keep their existing creation callbacks.
- WebGL adds the same floating toolbox shell plus a sandbox-owned role-add action.
- Browser validation must prove real add flows, not only visible markup.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Prepared validator passed`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `Planned`
